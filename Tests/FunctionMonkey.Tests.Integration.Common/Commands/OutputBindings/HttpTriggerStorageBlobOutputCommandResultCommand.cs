﻿using System;
using AzureFromTheTrenches.Commanding.Abstractions;
using FunctionMonkey.Tests.Integration.Common.Commands.TestInfrastructure;

namespace FunctionMonkey.Tests.Integration.Common.Commands.OutputBindings
{
    public class HttpTriggerStorageBlobOutputCommandResultCommand : ICommand<ServiceBusQueuedMarkerIdCommand>
    {
        public Guid MarkerId { get; set; }
    }
}
