﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace RedOutPlugin.Properties {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "17.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("RedOutPlugin.Properties.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to {&quot;Name&quot;:&quot;Default&quot;,&quot;components&quot;:[{&quot;constant&quot;:false,&quot;input_index&quot;:3,&quot;output_index&quot;:1,&quot;multiplierPos&quot;:0.12,&quot;multiplierNeg&quot;:0.06,&quot;offset&quot;:0.0,&quot;inverse&quot;:true,&quot;limit&quot;:-1.0,&quot;smoothing&quot;:1.0,&quot;enabled&quot;:true,&quot;math&quot;:null,&quot;spikeflatter&quot;:null,&quot;deadzone&quot;:0.0},{&quot;constant&quot;:false,&quot;input_index&quot;:4,&quot;output_index&quot;:2,&quot;multiplierPos&quot;:0.05,&quot;multiplierNeg&quot;:0.05,&quot;offset&quot;:0.0,&quot;inverse&quot;:false,&quot;limit&quot;:-1.0,&quot;smoothing&quot;:1.0,&quot;enabled&quot;:true,&quot;math&quot;:null,&quot;spikeflatter&quot;:null,&quot;deadzone&quot;:0.0}],&quot;effects&quot;:{&quot;effectID&quot;:2,&quot;inputID&quot;:4,&quot;multiplier&quot;:0.0 [rest of string was truncated]&quot;;.
        /// </summary>
        internal static string defProfile {
            get {
                return ResourceManager.GetString("defProfile", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to &lt;font color=&quot;red&quot;&gt;The game and GameEngine needs to run as admin for the plugin to work!&lt;/font&gt;.
        /// </summary>
        internal static string description {
            get {
                return ResourceManager.GetString("description", resourceCulture);
            }
        }
    }
}
