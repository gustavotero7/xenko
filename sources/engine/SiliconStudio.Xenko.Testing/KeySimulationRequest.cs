using SiliconStudio.Core;
using SiliconStudio.Xenko.Input;

namespace SiliconStudio.Xenko.Testing
{
    [DataContract]
    public class KeySimulationRequest
    {
        public Keys Key;
        public bool Down;
    }
}