﻿//-----------------------------------------------------------------------
// <copyright file="DistributorService.cs" company="Development In Progress Ltd">
//     Copyright © 2017. All rights reserved.
// </copyright>
// <author>Grant Colley</author>
//-----------------------------------------------------------------------

using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace DipDistributor
{
    public class DistributorService
    {
        public void Run()
        {
            var host = new WebHostBuilder()
                .UseKestrel()
                .UseUrls("http://+:5000")
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseIISIntegration()
                .UseStartup<Startup>()
                .UseApplicationInsights()
                .Build();

            host.Run();
        }
    }
}
