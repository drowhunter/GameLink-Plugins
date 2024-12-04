using System.ComponentModel;

namespace YawGLAPI
{

    public interface EventFeature : INotifyPropertyChanged
    {
        public void Invoke();
    }
    public interface IEventFeatureMap
    {
        public ARCADE_EVENT_TYPE EventId { get; set; }
        public EventFeature Feature { get; set; }
    }
}
