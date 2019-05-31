# Repro Steps

* Create a service bus resource in AZURE
* Create a topic named `lock`
* Create a subscriber named `default` (Max delivery count: `10`, Message Lock: `2minutes`)

* Replace `[CN]` and `[BUS]`

# Create some load

* Send messages to the topic (5000/10000)

# Run the application

* Launch the application

# Observations

* The subscriber starts processing the messages
* After 5000 messages are processed (and failed), subscriber stop processing further messages

# Gotchas

* When message fails, code doesn't call `subscriber.AbandonAsync`, in order to leverage the 2 minutes lock timeout, before re-processing.

# Expectation

1.  Even if `subscriber.AbandonAsync` is not called by consumer code, message gets automatically abandoned after timeout

https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-queues-topics-subscriptions

> If the application is unable to process the message for some reason, it can call the AbandonAsync method on the received message (instead of CompleteAsync). This method enables Service Bus to unlock the message and make it available to be received again, either by the same consumer or by another competing consumer. Secondly, there is a timeout associated with the lock and if the application fails to process the message before the lock timeout expires (for example, if the application crashes), then Service Bus unlocks the message and makes it available to be received again (essentially performing an AbandonAsync operation by default). 

2. Alternatively, as per [this docs](https://docs.microsoft.com/en-us/azure/service-bus-messaging/service-bus-quotas), we would expect service bus to throw `Number of concurrent connections on a namespace` exception to be thrown.