﻿using System;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using Uno.Extensions;
using Uno.Extensions.Configuration;
using Uno.Extensions.Navigation;
//using Chinook.DynamicMvvm;
//using Chinook.StackNavigation;
//using MallardMessageHandlers;

namespace ApplicationTemplate.Presentation
{
    public partial class HomePageViewModel : ViewModel
    {
        public IRouteMessenger Messenger { get; }
        public HomePageViewModel(IRouteMessenger messenger)
        {
            Messenger = messenger;
        }

        //public IDynamicCommand NavigateToPostsPage => this.GetCommandFromTask(async ct =>
        //{
        //    await this.GetService<IStackNavigator>().Navigate(ct, () => new PostsPageViewModel());
        //});
        public ICommand NavigateToPostsPage => new RelayCommand(() =>
   Messenger.Send(new RoutingMessage(this, typeof(PostsPageViewModel).Name.ToString())));

        //public IDynamicCommand NavigateToPostsPageWithNoNetwork => this.GetCommandFromTask(async ct =>
        //{
        //    await this.GetService<IStackNavigator>().Navigate(ct, () => new PostsPageViewModel(
        //        async () =>
        //        {
        //            await Task.Delay(TimeSpan.FromSeconds(2));

        //            throw new NoNetworkException("You don't have network.");
        //        })
        //    );
        //});
        public ICommand NavigateToPostsPageWithNoNetwork => new RelayCommand(() =>
   Messenger.Send(new RoutingMessage(this, typeof(PostsPageViewModel).Name.ToString())));

        //public IDynamicCommand NavigateToPostsPageWithOddError => this.GetCommandFromTask(async ct =>
        //{
        //    var executions = 0;

        //    await this.GetService<IStackNavigator>().Navigate(ct, () => new PostsPageViewModel(
        //        async () =>
        //        {
        //            await Task.Delay(TimeSpan.FromSeconds(2));

        //            if (executions++ % 2 != 0)
        //            {
        //                throw new NoNetworkException("You don't have network.");
        //            }
        //        }
        //    ));
        //});
        public ICommand NavigateToPostsPageWithOddError => new RelayCommand(() =>
   Messenger.Send(new RoutingMessage(this, typeof(PostsPageViewModel).Name.ToString())));

        //public IDynamicCommand NavigateToPostsPageWithEvenError => this.GetCommandFromTask(async ct =>
        //{
        //    var executions = 0;

        //    await this.GetService<IStackNavigator>().Navigate(ct, () => new PostsPageViewModel(
        //        async () =>
        //        {
        //            await Task.Delay(TimeSpan.FromSeconds(2));

        //            if (executions++ % 2 == 0)
        //            {
        //                throw new NoNetworkException("You don't have network.");
        //            }
        //        }
        //    ));
        //});
        public ICommand NavigateToPostsPageWithEvenError => new RelayCommand(() =>
   Messenger.Send(new RoutingMessage(this, typeof(PostsPageViewModel).Name.ToString())));

        //public IDynamicCommand NavigateToChuckNorrisSearchPage => this.GetCommandFromTask(async ct =>
        //{
        //    await this.GetService<IStackNavigator>().Navigate(ct, () => new ChuckNorrisSearchPageViewModel());
        //});
        public ICommand NavigateToChuckNorrisSearchPage => new RelayCommand(() =>
           Messenger.Send(new RoutingMessage(this, typeof(ChuckNorrisSearchPageViewModel).Name.ToString())));

        //public IDynamicCommand NavigateToSettingsPage => this.GetCommandFromTask(async ct =>
        //{
        //    await this.GetService<IStackNavigator>().Navigate(ct, () => new SettingsPageViewModel());
        //});

        public ICommand NavigateToSettingsPage => new RelayCommand(() =>
           Messenger.Send(new RoutingMessage(this, typeof(SettingsPageViewModel).Name.ToString())));
    }
}
