using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace YawGEAPI
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
