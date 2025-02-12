using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace YawGLAPI
{

    public enum MathType
    {

        ADD = 0,
        SUBSTRACT = 1,
        MULTIPLY = 2,
        DIVIDE = 3,
        ABS = 4,
        SQUARE = 5,
        SQRT = 6,
        LIMIT = 7,
        RECIPROCAL = 8
    }
    public enum ConditionOperator
    {
        GREATERTHAN,LESSTHAN,EQUALS,GREATERTHAN_EQ,LESSTHAN_EQ,
    }
    public enum ProfileComponentType
    {
        VALUE, DELTA, INCREMENTAL
    }

    public class LedEffect : INotifyPropertyChanged
    {
        private EFFECT_TYPE effectID;
        private int inputID;
        private float multiplier;
        private ObservableCollection<YawColor> colors = new ObservableCollection<YawColor>();

        public EFFECT_TYPE EffectID { get => effectID; set { effectID = value; OnPropertyChanged(); } }
        public int InputID { get => inputID; set { inputID = value; OnPropertyChanged(); } }
        public float Multiplier { get => multiplier; set { multiplier = value; OnPropertyChanged(); } }
        public ObservableCollection<YawColor> Colors { get => colors; set { colors = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;

        // Create the OnPropertyChanged method to raise the event
        // The calling member's name will be used as the parameter.
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }


        public LedEffect(EFFECT_TYPE effectID, int inputID,YawColor[] colors, float multiplier)
        {

            this.EffectID = effectID;
            this.InputID = inputID;
            this.Colors = new ObservableCollection<YawColor>(colors);
            this.Multiplier = multiplier;
        }
        public LedEffect()
        {

        }
    }


    public class ProfileMath : INotifyPropertyChanged
    {

        
        private MathType mathType;
        private int otherInputIndex;

        public MathType MathType { get => mathType; set { mathType = value; OnPropertyChanged(); } }
        public int  OtherInput {  get => otherInputIndex; set { otherInputIndex = value; OnPropertyChanged(); } }


        public event PropertyChangedEventHandler PropertyChanged;

        // Create the OnPropertyChanged method to raise the event
        // The calling member's name will be used as the parameter.
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
    public class ProfileCondition : INotifyPropertyChanged
    {
        private ConditionOperator conditionOperator = ConditionOperator.EQUALS;
        private int otherIndex = 0;
        private float _value;
        public ConditionOperator ComponentCondition { get { return conditionOperator; } set {  conditionOperator = value; OnPropertyChanged(); } }
        public int OtherIndex { get { return otherIndex; } set { otherIndex = value; OnPropertyChanged(); } }

        public float Value {  get { return _value; } set { _value = value; OnPropertyChanged(); } }



        public event PropertyChangedEventHandler PropertyChanged;

        // Create the OnPropertyChanged method to raise the event
        // The calling member's name will be used as the parameter.
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
    public class ProfileSpikeflatter : INotifyPropertyChanged
    {
        private bool enabled = false;
        private float limit;
        private float strength;

        public bool Enabled { get => enabled; set { enabled = value; OnPropertyChanged(); } }
        public float Limit { get => limit; set { limit = value; OnPropertyChanged(); }  }
        public float Strength { get => strength; set { strength = value; OnPropertyChanged(); } }

        public ProfileSpikeflatter()
        {
            this.enabled = false;
            this.strength = 0.5f;
            this.limit = 100;
        }
        public ProfileSpikeflatter(bool enabled, float limit, float strength)
        {
            this.limit = limit;
            this.strength = strength;
        }
        public event PropertyChangedEventHandler PropertyChanged;

        // Create the OnPropertyChanged method to raise the event
        // The calling member's name will be used as the parameter.
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

 

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class Profile_Component : INotifyPropertyChanged, ICloneable
    {
        private float multiplierPos;
        private float multiplierNeg;
        private int output_index;
        private int input_index;
        private bool constant = false;
        private bool inverse = false;
        private float limit;
        private float smoothing;
        private float offset;
        private bool enabled = true;
        private float deadzone = 0;
        private ProfileComponentType type = ProfileComponentType.VALUE;
        public bool Constant { get => constant; set { constant = value; OnPropertyChanged(); } }
        public int Input_index { get => input_index; set { input_index = value; OnPropertyChanged(); } }
        public int Output_index { get => output_index; set { output_index = value; OnPropertyChanged(); } }
        public float MultiplierPos { get => multiplierPos; set { multiplierPos = value; OnPropertyChanged(); } }
        public float MultiplierNeg { get => multiplierNeg; set { multiplierNeg = value; OnPropertyChanged(); } }
        public float Offset { get => offset; set { offset = value; OnPropertyChanged(); } }

        public bool Inverse { get => inverse; set { inverse = value; OnPropertyChanged(); } }
        public float Limit { get => limit; set { limit = value; OnPropertyChanged(); } }
        public float Smoothing { get => smoothing; set { smoothing = value; OnPropertyChanged(); } }

        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool Enabled { get { return enabled; } set { enabled = value; OnPropertyChanged(); } }
        
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public ProfileSpikeflatter Spikeflatter { get; set; } = new ProfileSpikeflatter();
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public float Deadzone { get { return deadzone; } set { deadzone = value; OnPropertyChanged(); } }
        
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public ProfileComponentType Type { get { return type; } set { type = value; OnPropertyChanged(); } }

      

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public ObservableCollection<ProfileCondition> Condition { get; set; } = new ObservableCollection<ProfileCondition>();

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public ObservableCollection<ProfileMath> Math { get; set; } = new ObservableCollection<ProfileMath>();

        public Profile_Component(int input_index, int output_index, float multiplierPos, float multiplierNeg, float offset, bool constant, bool inverse, float limit, float smoothing, bool enabled = true, ObservableCollection<ProfileMath> math = null, ProfileSpikeflatter spikeflatter = null, float deadzone = 0, ProfileComponentType type = ProfileComponentType.VALUE, ObservableCollection<ProfileCondition> condition = null)
        {
            this.Constant = constant;
            this.Input_index = input_index;
            this.Output_index = output_index;
            this.MultiplierPos = multiplierPos;
            this.MultiplierNeg = multiplierNeg;
            this.Offset = offset;
            this.Inverse = inverse;
            this.Limit = limit;
            this.Smoothing = smoothing;
            this.Enabled = enabled;
           
            this.Spikeflatter = spikeflatter is null ? new ProfileSpikeflatter() : spikeflatter;
            this.Deadzone = deadzone;
            this.Type = type;
            if(!(condition is null)) this.Condition = condition;
            if(!(math is null)) this.Math = math;
            

        }
        public Profile_Component()
        {
            this.Spikeflatter = new ProfileSpikeflatter();
            this.Math = new ObservableCollection<ProfileMath>();
            this.Condition = new ObservableCollection<ProfileCondition>();
        }
        public event PropertyChangedEventHandler PropertyChanged;

        // Create the OnPropertyChanged method to raise the event
        // The calling member's name will be used as the parameter.
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public object Clone()
        {
            return JsonConvert.DeserializeObject<Profile_Component>(JsonConvert.SerializeObject(this));
        }
        public override bool Equals(object obj)
        {
            if(obj is Profile_Component)
            {
                Profile_Component other = obj as Profile_Component;
                return JsonConvert.SerializeObject(this).Equals(JsonConvert.SerializeObject(other));
               
            } else
            {
                return false;
            }
        }
    }

}
