﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Microsoft.AspNetCore.SignalR.Tests
{
    public class ServerLogScope : IDisposable
    {
        private readonly ServerFixture _serverFixture;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IDisposable _wrappedDisposable;
        private readonly ConcurrentDictionary<string, ILogger> _serverLoggers;
        private readonly ILogger _scopeLogger;
        private readonly object _lock;

        public ServerLogScope(ServerFixture serverFixture, ILoggerFactory loggerFactory, IDisposable wrappedDisposable)
        {
            _loggerFactory = loggerFactory;
            _serverFixture = serverFixture;
            _wrappedDisposable = wrappedDisposable;

            _lock = new object();

            _serverLoggers = new ConcurrentDictionary<string, ILogger>(StringComparer.Ordinal);
            _scopeLogger = _loggerFactory.CreateLogger(nameof(ServerLogScope));

            // Attach last after everything else is initialized because a logged error can happen at any time
            _serverFixture.ServerLogged += ServerFixtureOnServerLogged;

            _scopeLogger.LogInformation("Server log scope started.");
        }

        private void ServerFixtureOnServerLogged(LogRecord logRecord)
        {
            var write = logRecord.Write;

            if (write == null)
            {
                _scopeLogger.LogWarning("Server log has no data.");
                return;
            }

            ILogger logger;

            // There maybe thready safety issues in logging when creating multiple loggers at the same time
            // https://github.com/aspnet/Logging/issues/810
            lock (_lock)
            {
                // Create (or get) a logger with the same name as the server logger
                logger = _serverLoggers.GetOrAdd(write.LoggerName, loggerName => _loggerFactory.CreateLogger("SERVER " + loggerName));
            }

            logger.Log(write.LogLevel, write.EventId, write.State, write.Exception, write.Formatter);
        }

        public void Dispose()
        {
            _serverFixture.ServerLogged -= ServerFixtureOnServerLogged;

            _scopeLogger.LogInformation("Server log scope stopped.");

            _wrappedDisposable?.Dispose();
        }
    }
}