﻿using System.Diagnostics;
using Uno.Extensions.Logging;

namespace Uno.Extensions.Navigation;

public class RouteNotifier : IRouteNotifier, IRouteUpdater
{
	public event EventHandler? RouteChanged;

	private ILogger Logger { get; }
	public RouteNotifier(ILogger<RouteNotifier> logger)
	{
		Logger = logger;
	}

	private int runningNavigations;

	private Stopwatch Timer { get; } = new();

	public void StartNavigation()
	{
		if(runningNavigations == 0)
		{
			Timer.Restart();
		}
		runningNavigations++;
	}

	public void EndNavigation()
	{
		runningNavigations--;
		if (runningNavigations == 0)
		{
			Timer.Stop();
			if (Logger.IsEnabled(LogLevel.Trace)) Logger.LogTraceMessage($"Elapsed navigation: {Timer.ElapsedMilliseconds}");
			RouteChanged?.Invoke(this, EventArgs.Empty);
		}
	}
}

