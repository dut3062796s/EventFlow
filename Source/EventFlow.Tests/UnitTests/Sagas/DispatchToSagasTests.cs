﻿// The MIT License (MIT)
//
// Copyright (c) 2015-2016 Rasmus Mikkelsen
// Copyright (c) 2015-2016 eBay Software Foundation
// https://github.com/rasmus/EventFlow
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
// FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
// IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Threading;
using System.Threading.Tasks;
using EventFlow.Aggregates;
using EventFlow.Configuration;
using EventFlow.Core;
using EventFlow.Sagas;
using EventFlow.TestHelpers;
using EventFlow.TestHelpers.Aggregates.Events;
using Moq;
using NUnit.Framework;

namespace EventFlow.Tests.UnitTests.Sagas
{
    [Category(Categories.Unit)]
    public class DispatchToSagasTests : TestsFor<DispatchToSagas>
    {
        private Mock<IResolver> _resolverMock;
        private Mock<ISagaStore> _sagaStoreMock;
        private Mock<ISagaErrorHandler> _sagaErrorHandlerMock;
        private Mock<ISagaDefinitionService> _sagaDefinitionServiceMock;
        private Mock<ISagaUpdater> _sagaUpdaterMock;
        private Mock<ISagaLocator> _sagaLocatorMock;

        [SetUp]
        public void SetUp()
        {
            var locatorType = A<Type>();

            var sagaType = typeof(ISaga);

            _resolverMock = InjectMock<IResolver>();
            _sagaStoreMock = InjectMock<ISagaStore>();
            _sagaErrorHandlerMock = InjectMock<ISagaErrorHandler>();
            _sagaDefinitionServiceMock = InjectMock<ISagaDefinitionService>();

            _sagaUpdaterMock = new Mock<ISagaUpdater>();
            _sagaLocatorMock = new Mock<ISagaLocator>();

            _resolverMock
                .Setup(r => r.Resolve(locatorType))
                .Returns(_sagaLocatorMock.Object);
            _resolverMock
                .Setup(r => r.Resolve(It.Is<Type>(t => typeof(ISagaUpdater).IsAssignableFrom(t))))
                .Returns(_sagaUpdaterMock.Object);
            _sagaDefinitionServiceMock
                .Setup(d => d.GetSagaDetails(It.IsAny<Type>()))
                .Returns(new[] {new SagaDetails(sagaType, locatorType, Many<Type>()),});
        }

        [Test]
        public async Task SagaUpdaterIsInvokedCorrectly()
        {
            // Arrange
            const int domainEventCount = 4;
            var sagaMock = Arrange_Woking_SagaStore(SagaState.Running);
            var domainEvents = ManyDomainEvents<ThingyPingEvent>(domainEventCount);

            // Act
            await Sut.ProcessAsync(domainEvents, CancellationToken.None).ConfigureAwait(false);

            // Assert
            _sagaUpdaterMock.Verify(
                u => u.ProcessAsync(sagaMock.Object, It.IsAny<IDomainEvent>(), It.IsAny<ISagaContext>(), It.IsAny<CancellationToken>()),
                Times.Exactly(domainEventCount));
        }

        private Mock<ISaga> Arrange_Woking_SagaStore(SagaState sagaState = SagaState.New)
        {
            var sagaMock = new Mock<ISaga>();

            sagaMock
                .Setup(s => s.State)
                .Returns(sagaState);

            _sagaStoreMock
                .Setup(s => s.UpdateAsync(
                    It.IsAny<ISagaId>(),
                    It.IsAny<SagaDetails>(),
                    It.IsAny<ISourceId>(),
                    It.IsAny<Func<ISaga, CancellationToken, Task>>(),
                    It.IsAny<CancellationToken>()))
                .Callback<ISagaId, SagaDetails, ISourceId, Func<ISaga, CancellationToken, Task>, CancellationToken>(
                    (id, details, arg3, arg4, arg5) => arg4(sagaMock.Object, CancellationToken.None))
                .ReturnsAsync(sagaMock.Object);

            return sagaMock;
        }
    }
}