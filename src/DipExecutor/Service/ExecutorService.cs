﻿//-----------------------------------------------------------------------
// <copyright file="ExecutorService.cs" company="Development In Progress Ltd">
//     Copyright © 2017. All rights reserved.
// </copyright>
// <author>Grant Colley</author>
//-----------------------------------------------------------------------

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;

namespace DipExecutor.Service
{
    /// <summary>
    /// Provides a WebHost for the <see cref="Executor"/>.
    /// </summary>
    public class ExecutorService : IExecutorService
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="url"></param>
        public void Run(string url)
        {
            var webHost = WebHost.CreateDefaultBuilder()
                .UseUrls(url)
                .UseStartup<Startup>()
                .Build();

            var task = webHost.RunAsync();
            task.GetAwaiter().GetResult();
        }
    }
}