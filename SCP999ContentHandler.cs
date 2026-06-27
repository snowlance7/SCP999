using Dusk;
using PSCPLibrary;
using UnityEngine;

namespace ItemSCPs
{
    public class SCP999ContentHandler : ContentHandler<SCP999ContentHandler>
    {
        public class SCP999Assets(DuskMod mod, string filePath) : AssetBundleLoader<SCP999Assets>(mod, filePath)
        {
            [LoadFromBundle("SCP999Info.asset")]
            public SCPInfo SCP999Info { get; private set; } = null!;
        }
        public SCP999Assets? SCP999;

        public class ContainmentJarAssets(DuskMod mod, string filePath) : AssetBundleLoader<ContainmentJarAssets>(mod, filePath) { }
        public ContainmentJarAssets? ContainmentJar;

        public SCP999ContentHandler(DuskMod mod) : base(mod)
        {
            RegisterContent("scp999", out SCP999);
            RegisterContent("containment_jar", out ContainmentJar);
        }
    }

}