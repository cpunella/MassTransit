﻿// Copyright 2007-2017 Chris Patterson, Dru Sellers, Travis Smith, et. al.
//  
// Licensed under the Apache License, Version 2.0 (the "License"); you may not use
// this file except in compliance with the License. You may obtain a copy of the 
// License at 
// 
//     http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software distributed
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR 
// CONDITIONS OF ANY KIND, either express or implied. See the License for the 
// specific language governing permissions and limitations under the License.
namespace MassTransit.AutomatonymousIntegration.Tests
{
    namespace Reschedule_Specs
    {
        using System;
        using System.Threading.Tasks;
        using Automatonymous;
        using Saga;
        using TestFramework;
        using NUnit.Framework;


        [TestFixture]
        public class Rescheduling_a_message_from_a_state_machine : StateMachineTestFixture
        {
            InMemorySagaRepository<TestState> _repository;
            TestStateMachine _machine;

            protected override void PreCreateBus(IInMemoryBusFactoryConfigurator configurator)
            {
                base.PreCreateBus(configurator);

                configurator.UseMessageScheduler(QuartzQueueAddress);
            }

            protected override void ConfigureInMemoryReceiveEndpoint(IInMemoryReceiveEndpointConfigurator configurator)
            {
                base.ConfigureInMemoryReceiveEndpoint(configurator);

                _repository = new InMemorySagaRepository<TestState>();

                _machine = new TestStateMachine();

                configurator.StateMachineSaga(_machine, _repository);
            }

            [Test]
            public async Task Should_reschedule_the_message_with_a_new_token_id()
            {
                Task<ConsumeContext<MessageRescheduled>> handler = SubscribeHandler<MessageRescheduled>();

                var correlationId = Guid.NewGuid();
                var startCommand = new StartCommand(correlationId);

                await InputQueueSendEndpoint.Send(startCommand);

                Guid? saga = await _repository.ShouldContainSaga(x => x.CorrelationId == correlationId, TestTimeout);
                Assert.IsTrue(saga.HasValue);
                var sagaInstance = _repository[saga.Value].Instance;

                ConsumeContext<MessageRescheduled> rescheduledEvent = await handler;

                Assert.NotNull(rescheduledEvent.Message.NewScheduleTokenId);
                Assert.AreEqual(sagaInstance.CorrelationId, rescheduledEvent.Message.CorrelationId);
                Assert.AreEqual(sagaInstance.ScheduleId, rescheduledEvent.Message.NewScheduleTokenId);
            }

            [Test]
            public async Task Should_unschedule_the_rescheduled_message_when_stop_command_arrived()
            {
                Task<ConsumeContext<MessageRescheduled>> handler = SubscribeHandler<MessageRescheduled>();

                var correlationId = Guid.NewGuid();
                var startCommand = new StartCommand(correlationId);

                await InputQueueSendEndpoint.Send(startCommand);

                Guid? saga = await _repository.ShouldContainSaga(x => x.CorrelationId == correlationId, TestTimeout);
                Assert.IsTrue(saga.HasValue);
                var sagaInstance = _repository[saga.Value].Instance;

                ConsumeContext<MessageRescheduled> rescheduledEvent = await handler;

                await InputQueueSendEndpoint.Send(new StopCommand(correlationId));

                saga = await _repository.ShouldNotContainSaga(correlationId, TestTimeout);

                Assert.IsNull(saga);
            }
        }

        #region Messages

        class Check
        {
            public Check(Guid correlationId)
            {
                CorrelationId = correlationId;
            }

            public Guid CorrelationId { get; set; }
        }


        class MessageRescheduled
        {
            public Guid CorrelationId { get; set; }
            public Guid? NewScheduleTokenId { get; set; }

            public MessageRescheduled(Guid correlationId, Guid? scheduleTokenId)
            {
                CorrelationId = correlationId;
                NewScheduleTokenId = scheduleTokenId;
            }
        }


        class StopCommand
        {
            public StopCommand(Guid correlationId)
            {
                CorrelationId = correlationId;
            }

            public Guid CorrelationId { get; set; }
        }


        class StartCommand
        {
            public StartCommand(Guid correlationId)
            {
                CorrelationId = correlationId;
            }

            public Guid CorrelationId { get; set; }
        }

        #endregion

        #region Saga

        class TestStateMachine : MassTransitStateMachine<TestState>
        {
            public TestStateMachine()
            {
                InstanceState(x => x.CurrentState);
                Event(() => StartCommand, x => x.CorrelateBy((s, m) => s.CorrelationId == m.Message.CorrelationId));
                Event(() => StopCommand, x => x.CorrelateBy((s, m) => s.CorrelationId == m.Message.CorrelationId));

                Schedule(() => ScheduledMessage, x => x.ScheduleId,
                    x =>
                    {
                        x.Delay = TimeSpan.FromSeconds(2);
                        x.Received = e => e.CorrelateById(context => context.Message.CorrelationId);
                    });

                Initially(
                    When(StartCommand)
                        .Then(x => x.Instance.CorrelationId = x.Data.CorrelationId)
                        .TransitionTo(Active));

                WhenEnter(Active, binder => binder
                    .Schedule(ScheduledMessage, x => new Check(x.Instance.CorrelationId)));

                During(Active,
                    When(ScheduledMessage.Received)
                        .Schedule(ScheduledMessage, x => new Check(x.Instance.CorrelationId))
                        .Publish(x => new MessageRescheduled(x.Instance.CorrelationId, x.Instance.ScheduleId)),
                    When(StopCommand)
                        .Unschedule(ScheduledMessage)
                        .Finalize());
                SetCompletedWhenFinalized();
            }

            public Event<StartCommand> StartCommand { get; private set; }
            public Event<StopCommand> StopCommand { get; private set; }
            public Schedule<TestState, Check> ScheduledMessage { get; private set; }
            public State Active { get; private set; }
        }


        class TestState : SagaStateMachineInstance
        {
            public Guid CorrelationId { get; set; }
            public string CurrentState { get; set; }
            public Guid? ScheduleId { get; set; }
        }

        #endregion
    }
}