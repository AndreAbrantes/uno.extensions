﻿namespace Uno.Extensions.Navigation.UI;

public abstract class ActionRequestHandlerBase<TView> : ControlRequestHandlerBase<TView>
	where TView : FrameworkElement
{
	private IRouteResolver RouteResolver { get; }
	protected ActionRequestHandlerBase(IRouteResolver routes)
	{
		RouteResolver = routes;
	}


	protected IRequestBinding? BindAction<TElement, TEventHandler>(
		TElement view,
		Func<Action<FrameworkElement>, TEventHandler> eventHandler,
		Action<TElement, TEventHandler> subscribe,
		Action<TElement, TEventHandler> unsubscribe
		)
		where TElement : FrameworkElement
	{
		var viewToBind = view;
		Action<FrameworkElement> action = async (element) =>
		{
			var path = element.GetRequest();
			var nav = element.Navigator();

			if (nav is null)
			{
				return;
			}

			var data = element.GetData();
			var resultType = data?.GetType();

			var binding = element.GetBindingExpression(Navigation.DataProperty);
			if (binding is not null &&
				binding.DataItem is not null)
			{
				var dataObject = binding.DataItem;
				var bindingPathSegments = binding.ParentBinding.Path.Path.Split('.').ToArray();
				for (int i = 0; i < bindingPathSegments.Length; i++)
				{
					var prop = dataObject.GetType().GetProperty(bindingPathSegments[i]);
					if (i == bindingPathSegments.Length - 1)
					{
						resultType = prop?.PropertyType;
					}
					else
					{
						dataObject = prop?.GetValue(dataObject);
					}

					if (dataObject is null)
					{
						break;
					}
				}
			}

			if (resultType is null && !string.IsNullOrWhiteSpace(path))
			{
				var routeMap = RouteResolver.FindByPath(path);
				resultType = routeMap?.ResultData;
			}


			if (data is not null ||
				resultType is not null)
			{

				if (resultType is not null)
				{
					var response = await nav.NavigateRouteForResultAsync(element, path, Schemes.Current, data, resultType: resultType);
					if (binding is not null &&
					binding.ParentBinding.Mode == BindingMode.TwoWay)
					{
						if (response is not null)
						{
							var result = await response.UntypedResult;
							if (result.IsSome(out var resultValue))
							{
								element.SetData(resultValue);
								binding.UpdateSource();
							}
						}
					}
				}
				else
				{
					await nav.NavigateRouteAsync(element, path, Schemes.Current, data);

				}
			}
			else
			{
				await nav.NavigateRouteAsync(element, path, Schemes.Current);
			}
		};

		var handler = eventHandler(action);

		bool subscribed = false;
		if (view.IsLoaded)
		{
			subscribed = true;
			subscribe(viewToBind, handler);
		}

		RoutedEventHandler loadedHandler = (s, e) =>
		{
			if (!subscribed)
			{
				subscribed = true;
				subscribe(viewToBind, handler);
			}
		};
		view.Loaded += loadedHandler;
		RoutedEventHandler unloadedHandler = (s, e) =>
		{
			if (subscribed)
			{
				subscribed = false;
				unsubscribe(viewToBind, handler);
			}
		};
		view.Unloaded += unloadedHandler;

		return new RequestBinding(viewToBind, loadedHandler, unloadedHandler);
	}
}
