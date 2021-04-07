namespace MediatR.Tests.Pipeline
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using MediatR.Pipeline;
    using Shouldly;
    using StructureMap;
    using Xunit;

    public class RequestExceptionHandlerTests
    {
        public class Ping : IRequest<Pong>
        {
            public string Message { get; set; }
        }

        public class Pong
        {
            public string Message { get; set; }
        }

        public class PingException : Exception
        {
            public PingException(string message) : base(message + " Thrown")
            {
            }
        }

        public class PingHandler : IRequestHandler<Ping, Pong>
        {
            public Task<Pong> Handle(Ping request, CancellationToken cancellationToken)
            {
                throw new PingException(request.Message);
            }
        }

        public class PingPongExceptionHandlerForType : IRequestExceptionHandler<Ping, Pong, PingException>
        {
            public Task Handle(Ping request, PingException exception, RequestExceptionHandlerState<Pong> state, CancellationToken cancellationToken)
            {
                state.SetHandled(new Pong() { Message = exception.Message + " Handled by Type" });

                return Task.CompletedTask;
            }
        }

        public class PingPongExceptionHandler : RequestExceptionHandler<Ping, Pong>
        {
            protected override void Handle(Ping request, Exception exception, RequestExceptionHandlerState<Pong> state)
            {
                state.SetHandled(new Pong() { Message = exception.Message + " Handled"});
            }
        }

        public class PingPongExceptionHandlerNotHandled : RequestExceptionHandler<Ping, Pong>
        {
            protected override void Handle(Ping request, Exception exception, RequestExceptionHandlerState<Pong> state)
            {
                request.Message = exception.Message + " Not Handled";
            }
        }

        public class PingPongThrowingExceptionHandler : RequestExceptionHandler<Ping, Pong>
        {
            protected override void Handle(Ping request, Exception exception, RequestExceptionHandlerState<Pong> state)
            {
                throw new ApplicationException("Surprise!");
            }
        }

        [Fact]
        public async Task Should_run_exception_handler_and_allow_for_exception_not_to_throw()
        {
            var container = new Container(cfg =>
            {
                cfg.For<IRequestHandler<Ping, Pong>>().Use<PingHandler>();
                cfg.For<IRequestExceptionHandler<Ping, Pong, Exception>>().Use<PingPongExceptionHandler>();
                cfg.For<IRequestExceptionHandler<Ping, Pong, PingException>>().Use<PingPongExceptionHandlerForType>();
                cfg.For(typeof(IPipelineBehavior<,>)).Add(typeof(RequestExceptionProcessorBehavior<,>));
                cfg.For<ServiceFactory>().Use<ServiceFactory>(ctx => t => ctx.GetInstance(t));
                cfg.For<IMediator>().Use<Mediator>();
            });

            var mediator = container.GetInstance<IMediator>();

            var response = await mediator.Send(new Ping { Message = "Ping" });

            response.Message.ShouldBe("Ping Thrown Handled by Type");
        }

        [Fact]
        public async Task Should_run_exception_handler_and_allow_for_exception_to_be_still_thrown()
        {
            var container = new Container(cfg =>
            {
                cfg.For<IRequestHandler<Ping, Pong>>().Use<PingHandler>();
                cfg.For<IRequestExceptionHandler<Ping, Pong, Exception>>().Use<PingPongExceptionHandlerNotHandled>();
                cfg.For(typeof(IPipelineBehavior<,>)).Add(typeof(RequestExceptionProcessorBehavior<,>));
                cfg.For<ServiceFactory>().Use<ServiceFactory>(ctx => t => ctx.GetInstance(t));
                cfg.For<IMediator>().Use<Mediator>();
            });

            var mediator = container.GetInstance<IMediator>();

            var request = new Ping { Message = "Ping" };
            await Should.ThrowAsync<PingException>(async () =>
            {
                await mediator.Send(request);
            });

            request.Message.ShouldBe("Ping Thrown Not Handled");
        }

        [Fact]
        public async Task Should_run_exception_handler_and_unwrap_expections_thrown_in_the_handler()
        {
            var container = new Container(cfg =>
            {
                cfg.For<IRequestHandler<Ping, Pong>>().Use<PingHandler>();
                cfg.For<IRequestExceptionHandler<Ping, Pong, Exception>>().Use<PingPongThrowingExceptionHandler>();
                cfg.For(typeof(IPipelineBehavior<,>)).Add(typeof(RequestExceptionProcessorBehavior<,>));
                cfg.For<ServiceFactory>().Use<ServiceFactory>(ctx => t => ctx.GetInstance(t));
                cfg.For<IMediator>().Use<Mediator>();
            });

            var mediator = container.GetInstance<IMediator>();

            var request = new Ping { Message = "Ping" };
            await Should.ThrowAsync<ApplicationException>(async () =>
            {
                await mediator.Send(request);
            });
        }

    }
}