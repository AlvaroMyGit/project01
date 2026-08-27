using Xunit;
using StalkerALifeSandbox.Core;

namespace StalkerALifeSandbox.Tests;

public class EventBusTests
{
    private struct TestEvent { public int Value; }

    [Fact]
    public void CanSubscribeAndPublish()
    {
        EventBus.ClearAll();
        
        int received = 0;
        EventBus.Subscribe<TestEvent>(e => received = e.Value);
        
        EventBus.Publish(new TestEvent { Value = 42 });
        
        Assert.Equal(42, received);
    }
    
    [Fact]
    public void CanUnsubscribe()
    {
        EventBus.ClearAll();
        
        int received = 0;
        Action<TestEvent> handler = e => received = e.Value;
        
        EventBus.Subscribe(handler);
        EventBus.Unsubscribe(handler);
        
        EventBus.Publish(new TestEvent { Value = 42 });
        
        Assert.Equal(0, received);
    }
}
