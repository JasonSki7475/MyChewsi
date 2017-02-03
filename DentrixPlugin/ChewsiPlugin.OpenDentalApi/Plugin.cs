using System.Windows.Forms;
using OpenDentBusiness;

namespace ChewsiPlugin.OpenDentalApi
{
    /// <summary>
    ///  Required class.  Don't change the name.
    ///  The namespace for this class must match the dll filename, including capitalization.  
    ///  All other classes will typically belong to the same namespace too, but that's not a requirement.
    /// </summary>
    /// <seealso cref="OpenDentBusiness.PluginBase" />
    public class Plugin : PluginBase
    {
        private Form host;

        ///<summary></summary>
        public override Form Host
        {
            get { return host; }
            set
            {
                host = value;

                //ConvertPluginDatabase.Begin();//if this crashes, it will bubble up and result in the plugin not loading.
                //If the plugin is only for personal use, then data tables do not need to be managed in the code.
                //They could instead be managed manually using a tool.
            }
        }
    }
}