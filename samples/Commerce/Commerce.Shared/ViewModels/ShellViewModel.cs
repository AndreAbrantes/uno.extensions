﻿using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Uno.Extensions.Navigation;
using Uno.Extensions.Hosting;
using Uno.Extensions.Configuration;
using System;

namespace Commerce.ViewModels
{
	public class ShellViewModel
	{
		private INavigator Navigator { get; }

		private IWritableOptions<Credentials> CredentialsSettings { get; }

		public ShellViewModel(
			ILogger<ShellViewModel> logger,
			INavigator navigator,
			IOptions<HostConfiguration> configuration,
			IWritableOptions<Credentials> credentials)
		{
			Navigator = navigator;
			CredentialsSettings = credentials;

			if (logger.IsEnabled(LogLevel.Information)) logger.LogInformation($"Launch url '{configuration.Value?.LaunchUrl}'");
			var initialRoute = configuration.Value?.LaunchRoute();


			// Go to the login page on app startup
			Start(initialRoute);
		}

		public async Task Start(Route? initialRoute = null)
		{
			var currentCredentials = CredentialsSettings.Value;

			if (currentCredentials?.UserName is { Length: > 0 })
			{
				if (initialRoute is not null &&
					!initialRoute.IsEmpty())
				{
					var initialResponse = await Navigator.NavigateRouteForResultAsync<object>(this, initialRoute);
					_ = await initialResponse.Result;
				}
				else
				{
					var homeResponse = await Navigator.NavigateDataForResultAsync<Credentials, object>(this, currentCredentials, Schemes.ClearBackStack);
					_ = await homeResponse.Result;
				}

				await CredentialsSettings.Update(c => new Credentials());
			}
			else
			{
				// Navigate to Login page, requesting Credentials
				var response = await Navigator.NavigateForResultAsync<Credentials>(this, Schemes.ClearBackStack);


				var loginResult = await response.Result;
				if (loginResult.IsSome(out var creds) && creds?.UserName is { Length: > 0 })
				{
					await CredentialsSettings.Update(c => creds);
				}
			}

			// At this point we assume that either the login failed, or that the navigation to home
			// was completed by a response being sent back. We don't actually care about the response,
			// since we only care that the navigation has completed and that we should again show the login 
			Start();
		}
	}
}
