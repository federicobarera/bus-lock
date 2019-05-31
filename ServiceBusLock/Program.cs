using Microsoft.Azure.ServiceBus;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ServiceBusLock
{
    class Program
    {
        const string CN = "[CN]";
        const string BUS_URI = "sb://[BUS].servicebus.windows.net";
        static readonly TimeSpan RETRY_MIN_BACKOFF = TimeSpan.FromSeconds(1);
        static readonly TimeSpan RETRY_MAX_BACKOFF = TimeSpan.FromSeconds(30);
        const int RETRY_COUNT = 3;
        const int RECEIVER_MAX_CONCURRENCY = 5;
        const bool AUTO_COMPLETE = false;

        /***************************************/
        /**********SUBSCRIPTION DETAILS*********/
        /*
                * Max delivery count: 10
                * Message Lock Duration: 2minutes
        */
        /***************************************/
        const string TOPIC_NAME = "lock";
        const string SUBSCRIPTION_NAME = "default";

        const bool MONITOR_VERBOSE = true;
        const int MONITOR_TOP_TAKE = 10;

        static readonly ConcurrentBag<Tuple<string, string>> messageRefs = new ConcurrentBag<Tuple<string, string>>();

        static void Main(string[] args)
        {
            /*
                * Subscription Details
                * Max delivery count: 10
                * Lock Duration: 2minutes
            */

            var receiver = CreateMessageReceiver();
            var options = new MessageHandlerOptions(ExceptionReceivedHandler)
            {
                MaxConcurrentCalls = 5,
                AutoComplete = false,
            };

            receiver.RegisterMessageHandler(async (message, cancellation) =>
            {
                try
                {
                    messageRefs.Add(new Tuple<string, string>(message.MessageId.ToString(), message.SystemProperties.LockToken));

                    throw new Exception("something bad happens...");
                    await receiver.CompleteAsync(message.SystemProperties.LockToken);
                }
                catch (Exception)
                {
                    //we do not want to return the message immediately (receiver.AbandonAsync)
                    //let the message lock (2min) expire before this message being re-processed
                }
            }, options);

            while (true) //monitor
            {
                Console.WriteLine($"Message read: {messageRefs.Count}");
                if (MONITOR_VERBOSE)
                    Console.WriteLine(
                        String.Join(
                            Environment.NewLine,
                            messageRefs
                                .GroupBy(x => x.Item1)
                                .OrderByDescending(x => x.Count())
                                .Take(MONITOR_TOP_TAKE)
                                .Select(x => $"Id: {x.Key} | Locks: {x.Count()}")));
                Thread.Sleep(5000);
            }
        }

        static Task ExceptionReceivedHandler(ExceptionReceivedEventArgs exceptionReceivedEventArgs)
        {
            Console.WriteLine($"Message handler encountered an exception {exceptionReceivedEventArgs.Exception}.");
            var context = exceptionReceivedEventArgs.ExceptionReceivedContext;
            Console.WriteLine("Exception context for troubleshooting:");
            Console.WriteLine($"- Endpoint: {context.Endpoint}");
            Console.WriteLine($"- Entity Path: {context.EntityPath}");
            Console.WriteLine($"- Executing Action: {context.Action}");
            return Task.CompletedTask;
        }

        private static void PrintReport()
        {
            Console.WriteLine($"Message read: {messageRefs.Count}");
            if (MONITOR_VERBOSE)
                Console.WriteLine(
                    String.Join(
                        Environment.NewLine,
                        messageRefs
                            .GroupBy(x => x.Item1)
                            .OrderByDescending(x => x.Count())
                            .Take(MONITOR_TOP_TAKE)
                            .Select(x => $"Id: {x.Key} | Locks: {x.Count()}")));
        }

        static Task DoWork(Message message)
        {
            /* ... */
            messageRefs.Add(new Tuple<string, string>(message.MessageId.ToString(), message.SystemProperties.LockToken));
            throw new Exception("something bad happens...");
        }

        private static SubscriptionClient CreateMessageReceiver()
        {
            var retryPolicy = new RetryExponential(
                RETRY_MIN_BACKOFF,
                RETRY_MAX_BACKOFF,
                RETRY_COUNT);

            var subscriptionClient = new SubscriptionClient(CN, TOPIC_NAME, SUBSCRIPTION_NAME);

            return subscriptionClient;
        }
    }
}
