﻿using Uno.Extensions.Navigation.Regions;

namespace Uno.Extensions.Navigation;

public class Navigator : INavigator, IInstance<IServiceProvider>
{
	protected ILogger Logger { get; }

	protected IRegion Region { get; }

	private IRouteUpdater? RouteUpdater => Region.Services?.GetRequiredService<IRouteUpdater>();

	IServiceProvider? IInstance<IServiceProvider>.Instance => Region.Services;

	public Route? Route { get; protected set; }

	protected IRouteResolver RouteResolver { get; }

	public Navigator(ILogger<Navigator> logger, IRegion region, IRouteResolver routeResolver)
		: this((ILogger)logger, region, routeResolver)
	{
	}

	protected Navigator(ILogger logger, IRegion region, IRouteResolver routeResolver)
	{
		Region = region;
		Logger = logger;
		RouteResolver = routeResolver;
	}

	public async Task<NavigationResponse?> NavigateAsync(NavigationRequest request)
	{
		if (Logger.IsEnabled(LogLevel.Information)) Logger.LogInformation($"Pre-navigation: - {Region.Root().ToString()}");
		try
		{
			RouteUpdater?.StartNavigation();

			// Initialise the region
			var requestMap = RouteResolver.FindByPath(request.Route.Base);
			if (requestMap?.Init is not null)
			{
				var newRequest = requestMap.Init(request);
				while (!request.SameRouteBase(newRequest))
				{
					request = newRequest;
					requestMap = RouteResolver.FindByPath(request.Route.Base);
					if (requestMap?.Init is not null)
					{
						newRequest = requestMap.Init(request);
					}
				}
				request = newRequest;
			}

			// Handle root navigations
			if (request.Route.IsRoot())
			{
				// Either
				// - forward to parent (if parent is not null)
				// - trim the Root scheme ready for handling
				if (Region.Parent is not null)
				{
					return await (Region.Parent?.NavigateAsync(request) ?? Task.FromResult<NavigationResponse?>(default));
				}
				else
				{
					// This is the root nav service - need to pass the
					// request down to children by making the request nested
					request = request with { Route = request.Route.TrimScheme(Schemes.Root) };
				}
			}

			// Request for parent (ignore the first layer of parent scheme)
			if (request.Route.IsParent())
			{
				request = request with { Route = request.Route.TrimScheme(Schemes.Parent) };

				// Handle parent navigations
				if (request.Route.IsParent())
				{
					return await (Region.Parent?.NavigateAsync(request) ?? Task.FromResult<NavigationResponse?>(default));
				}
			}

			// Is this region is an unnamed child of a composite,
			// send request to parent if the route has no scheme
			if (request.Route.IsCurrent() &&
				!Region.IsNamed() &&
				Region.Parent is not null
				&& !(Region.Children.Any(x => x.Name == request.Route.Base))
				)
			{
				return await Region.Parent.NavigateAsync(request);
			}


			// Run dialog requests
			if (request.Route.IsDialog())
			{
				return await DialogNavigateAsync(request);
			}

			// If the base matches the region name, than need to strip the base
			if (!string.IsNullOrWhiteSpace(request.Route.Base) &&
				request.Route.Base == Region.Name &&
				!CanNavigateToRoute(request.Route.TrimScheme(Schemes.Nested)))
			{
				request = request with { Route = request.Route.Next() };
			}

			// Make sure the view has completely loaded before trying to process the nav request
			// Typically this might happen with the first navigation of the application where the
			// window hasn't been activated yet, so the root region may not have loaded
			await Region.View.EnsureLoaded();

			return await ResponseNavigateAsync(request);
		}
		finally
		{
			if (Logger.IsEnabled(LogLevel.Information)) Logger.LogInformation($"Post-navigation: {Region.Root().ToString()}");
			if (Logger.IsEnabled(LogLevel.Information)) Logger.LogInformation($"Post-navigation (route): {Region.Root().GetRoute()}");
			RouteUpdater?.EndNavigation();
		}
	}

	protected virtual bool CanNavigateToRoute(Route route) => route.IsCurrent();

	private async Task<NavigationResponse?> DialogNavigateAsync(NavigationRequest request)
	{
		var dialogService = Region.Services?.GetService<INavigatorFactory>()?.CreateService(Region, request);

		// Trim dialog scheme to prevent recursion when we call Navigate
		request = request with { Route = request.Route with { Scheme = Schemes.Current } };

		var dialogResponse = await (dialogService?.NavigateAsync(request) ?? Task.FromResult<NavigationResponse?>(default));

		return dialogResponse;
	}

	private async Task<NavigationResponse?> ResponseNavigateAsync(NavigationRequest request)
	{
		var services = Region.Services;
		if (services is null)
		{
			return default;
		}

		var mapping = RouteResolver.Find(request.Route);
		if (mapping?.UntypedToQuery is not null)
		{
			request = request with { Route = request.Route with { Data = request.Route.Data?.AsParameters(mapping) } };
		}

		// Setup the navigation data (eg parameters to be injected into viewmodel)
		var dataFactor = services.GetRequiredService<NavigationDataProvider>();
		dataFactor.Parameters = (request.Route?.Data) ?? new Dictionary<string, object>();

		var responseFactory = services.GetRequiredService<IResponseNavigatorFactory>();
		// Create ResponseNavigator if result is requested
		var navigator = request.Result is not null ? request.GetResponseNavigator(responseFactory, this) : default;

		if (navigator is null)
		{
			// Since this navigation isn't requesting a response, make sure
			// the current INavigator is this navigator. This will have override
			// any responsenavigator that has been registered and avoid incorrectly
			// sending a response when simply navigating back
			services.AddInstance<INavigator>(this);
		}

		var executedRoute = await CoreNavigateAsync(request);


		if (navigator is not null)
		{
			return navigator.AsResponseWithResult(executedRoute);
		}


		return executedRoute;

	}

	protected virtual async Task<NavigationResponse?> CoreNavigateAsync(NavigationRequest request)
	{
		if (request.Route.IsCurrent() || request.Route.IsBackOrCloseNavigation())
		{
			request = request with { Route = request.Route.AppendScheme(Schemes.Nested) };
		}

		if (request.Route.IsEmpty())
		{
			var route = RouteResolver.FindByPath(this.Route?.Base);
			if (route is not null)
			{
				var defaultRoute = route.Nested?.FirstOrDefault(x => x.IsDefault);
				if (defaultRoute is not null)
				{
					request = request with { Route = request.Route.Append(defaultRoute.Path) };

				}
			}

			if (request.Route.IsEmpty())
			{
				return null;
			}
		}

		if (!string.IsNullOrWhiteSpace(Region.Name) && request.Result is not null)
		{
			request = request with { Result = null };
		}


		var children = Region.Children.Where(region =>
										// Unnamed child regions
										string.IsNullOrWhiteSpace(region.Name) ||
										// Regions whose name matches the next route segment
										region.Name == request.Route.Base ||
										// Regions whose name matches the current route
										// eg currently selected tab
										region.Name == Route?.Base
									).ToArray();

		var tasks = new List<Task<NavigationResponse?>>();
		foreach (var region in children)
		{
			tasks.Add(region.NavigateAsync(request));
		}

		await Task.WhenAll(tasks);
#pragma warning disable CA1849 // We've already waited all tasks at this point (see Task.WhenAll in line above)
		return tasks.FirstOrDefault(r => r.Result is not null)?.Result;
#pragma warning restore CA1849
	}

	public override string ToString()
	{
		var current = NavigatorToString;
		if (!string.IsNullOrWhiteSpace(current))
		{
			current = $"({current})";
		}
		return $"{this.GetType().Name}{current}";
	}

	protected virtual string NavigatorToString { get; } = string.Empty;

	private object? CreateDefaultViewModel()
	{
		if (Region.View is null)
		{
			return null;
		}

		var services = Region.Services;

		// Make sure the navigator is in the services so it can be used
		// when creating the view model
		services?.AddInstance<INavigator>(this);

		var mapping = RouteResolver.FindByView(Region.View.GetType());
		if (mapping?.ViewModel is not null)
		{
			var vm = services?.GetService(mapping.ViewModel);
			if (vm is IInjectable<INavigator> navAware)
			{
				navAware.Inject(this);
			}

			if (vm is IInjectable<IServiceProvider> spAware && Region.Services is not null)
			{
				spAware.Inject(Region.Services);
			}

			return vm;
		}

		return null;
	}
}
