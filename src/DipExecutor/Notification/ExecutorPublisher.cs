﻿//-----------------------------------------------------------------------
// <copyright file="ExecutorPublisher.cs" company="Development In Progress Ltd">
//     Copyright © 2017. All rights reserved.
// </copyright>
// <author>Grant Colley</author>
//-----------------------------------------------------------------------

using DipRunner;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DipExecutor.Notification
{
    public class ExecutorPublisher : BatchNotifier<IEnumerable<StepNotification>>, IBatchNotifier<IEnumerable<StepNotification>>
    {
        private readonly INotificationPublisher notificationPublisher;

        public ExecutorPublisher(INotificationPublisher notificationPublisher)
        {
            this.notificationPublisher = notificationPublisher;

            Start();
        }

        public override async Task NotifyAsync(IEnumerable<IEnumerable<StepNotification>> notifications, CancellationToken cancellationToken)
        {
            foreach(var notificationsBatch in notifications)
            {
                await notificationPublisher.PublishAsync(notificationsBatch);
            }
        }
    }
}