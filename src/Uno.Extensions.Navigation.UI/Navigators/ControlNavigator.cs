﻿using System.Reflection;
using Uno.Extensions.Logging;
using Uno.Extensions.Navigation.Regions;
using Uno.Extensions.Navigation.UI.Controls;

namespace Uno.Extensions.Navigation.Navigators;

public abstract class ControlNavigator<TControl> : ControlNavigator
	where TControl : class
{
	public virtual TControl? Control { get; }

	protected ControlNavigator(
		ILogger logger,
		IRegion region,
		IRouteResolver routeResolver,
		TControl? control)
		: base(logger, routeResolver, region)
	{
		Control = control;
	}

	protected override DispatcherQueue GetDispatcher() =>
#if WINUI
		(Control as FrameworkElement)?.DispatcherQueue
#else
		Windows.ApplicationModel.Core.CoreApplication.MainView.DispatcherQueue
#endif
		?? base.GetDispatcher();

	protected virtual FrameworkElement? CurrentView => default;

	protected abstract Task<string?> Show(string? path, Type? viewType, object? data);

	protected override async Task<Route?> ExecuteRequestAsync(NavigationRequest request)
	{
		if (Control is null)
		{
			return default;
		}

        var route = request.Route;
        var mapping = RouteResolver.Find(route);
		if(Logger.IsEnabled(LogLevel.Debug)) Logger.LogDebugMessage($"Navigating to path '{route.Base}' with view '{mapping?.View?.Name}'");
        var executedPath = await Show(route.Base, mapping?.View, route.Data);

		if (string.IsNullOrEmpty(executedPath))
		{
			return Route.Empty;
		}

		var executedRoute = route with { Base = executedPath, Path = null };

		InitialiseCurrentView(executedRoute, mapping);

		return executedRoute;
	}

	protected object? InitialiseCurrentView(Route route, RouteMap? mapping)
	{
		var view = CurrentView;

		if (view is null)
		{
			return null;
		}

		var navigator = Region.Navigator();

		if (view is FrameView fv )
		{
			navigator = fv.Navigator;
		}


		var services = navigator?.Get<IServiceProvider>();

		if (navigator is null ||
			services is null)
		{
			return null;
		}

		var viewModel = view.DataContext;
		if (viewModel is null ||
			viewModel.GetType() != mapping?.ViewModel)
		{
			// This will happen if cache mode isn't set to required
			viewModel = CreateViewModel(services, route, mapping);
		}

		view.InjectServicesAndSetDataContext(services, navigator, viewModel);

		return viewModel;
	}


	protected override string NavigatorToString => (Route?.ToString()) ?? string.Empty;
}

public abstract class ControlNavigator : Navigator
{
	public virtual bool CanGoBack => false;

	protected ControlNavigator(
		ILogger logger,
		IRouteResolver routeResolver,
		IRegion region)
		: base(logger, region, routeResolver)
	{
	}

	protected async override Task<NavigationResponse?> CoreNavigateAsync(NavigationRequest request)
	{
		var regionResponse = await RegionNavigateAsync(request);

		if (regionResponse is not null)
		{
			if (!regionResponse.Success)
			{
				return regionResponse;
			}

			request = request with { Route = request.Route.Trim(regionResponse?.Route) };
		}

		var coreResponse = await base.CoreNavigateAsync(request);

		return coreResponse ?? regionResponse;
	}

	private async Task<NavigationResponse?> RegionNavigateAsync(NavigationRequest request)
	{
		// If the request has come down from parent it
		// will still have the ./ prefix, so need to trim
		// it before processing it
		if (request.Route.IsNested())
		{
			request = request with { Route = request.Route.TrimScheme(Schemes.Nested) };
		}

		var completion = new TaskCompletionSource<NavigationResponse?>();
		GetDispatcher().TryEnqueue(async () =>
		{
			if (CanNavigateToRoute(request.Route))
			{
				var response = await ControlNavigateAsync(request);
				completion.SetResult(response);
				return;
			}
			completion.SetResult(default);
		});

		return await completion.Task;
	}

	public virtual void ControlInitialize()
	{
	}

	protected virtual DispatcherQueue GetDispatcher() => DispatcherQueue.GetForCurrentThread();

	protected async Task<NavigationResponse?> ControlNavigateAsync(NavigationRequest request)
	{
		var services = Region.Services;
		if (services is null)
		{
			return default;
		}

		var executedRoute = await ExecuteRequestAsync(request);

		UpdateRoute(executedRoute);

		return new NavigationResponse(executedRoute ?? Route.Empty);
	}

	protected virtual void UpdateRoute(Route? route)
	{
		Route = route is not null ? new Route(Schemes.Current, route.Base, null, route.Data) : null;
	}

	protected object? CreateViewModel(IServiceProvider services, Route route, RouteMap? mapping)
	{
		var navigator = services.GetInstance<INavigator>();
		if (mapping?.ViewModel is not null)
		{
			var dataFactor = services.GetRequiredService<NavigationDataProvider>();
			dataFactor.Parameters = route.Data ?? new Dictionary<string, object>();

			var vm = services.GetService(mapping.ViewModel);

			if (vm is null)
			{
				try
				{
					var ctr = mapping.ViewModel.GetNavigationConstructor(navigator!, Region.Services!, out var args);
					if (ctr is not null)
					{
						vm = ctr.Invoke(args);
					}
				}
				catch
				{
					Logger.LogInformationMessage("ViewModel not included in RouteMap, and unable to instance using Activator instead of ServiceProvider");
				}
			}

			if (vm is IInjectable<INavigator> navAware)
			{
				navAware.Inject(navigator!);
			}

			if (vm is IInjectable<IServiceProvider> spAware && Region.Services is not null)
			{
				spAware.Inject(Region.Services);
			}

			return vm;
		}

		return null;
	}

	protected abstract Task<Route?> ExecuteRequestAsync(NavigationRequest request);
}
