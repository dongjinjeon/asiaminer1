﻿using NHM.Common;
using NHM.Common.Algorithm;
using NHM.Common.Device;
using NHM.Common.Enums;
using NHM.MinerPluginToolkitV1;
using NHM.MinerPluginToolkitV1.Configs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Excavator
{
    public partial class ExcavatorPlugin : PluginBase
    {
        public ExcavatorPlugin()
        {
            // mandatory init
            InitInsideConstuctorPluginSupportedAlgorithmsSettings();
            // set default internal settings
            MinerOptionsPackage = PluginInternalSettings.MinerOptionsPackage;
            DefaultTimeout = PluginInternalSettings.DefaultTimeout;
            GetApiMaxTimeoutConfig = PluginInternalSettings.GetApiMaxTimeoutConfig;
            MinerBenchmarkTimeSettings = PluginInternalSettings.BenchmarkTimeSettings;
            // TODO link
            MinersBinsUrlsSettings = new MinersBinsUrlsSettings
            {
                BinVersion = "v1.6.2a",
                ExePath = new List<string> { "excavator.exe" },
                Urls = new List<string>
                {
                    "https://github.com/nicehash/excavator/releases/download/v1.6.2a/excavator_1.6.2a_build514_Win64.zip"
                }
            };
            PluginMetaInfo = new PluginMetaInfo
            {
                PluginDescription = "Excavator NVIDIA GPU miner from NiceHash",
                SupportedDevicesAlgorithms = SupportedDevicesAlgorithmsDict()
            };
        }

        public override Version Version => new Version(15, 7);

        public override string Name => "Excavator";

        public override string Author => "info@nicehash.com";

        public override string PluginUUID => "27315fe0-3b03-11eb-b105-8d43d5bd63be";

        public override Dictionary<BaseDevice, IReadOnlyList<Algorithm>> GetSupportedAlgorithms(IEnumerable<BaseDevice> devices)
        {
            // SM 6.0+
            var cudaGpus = devices.Where(dev => dev is CUDADevice cuda && cuda.SM_major >= 6).Cast<CUDADevice>();
            var supported = new Dictionary<BaseDevice, IReadOnlyList<Algorithm>>();
            var minDrivers = new Version(411, 0); // TODO
            if (CUDADevice.INSTALLED_NVIDIA_DRIVERS < minDrivers) return supported;

            foreach (var gpu in cudaGpus)
            {
                var algos = GetSupportedAlgorithmsForDevice(gpu);
                if (algos.Count > 0) supported.Add(gpu, algos);
            }

            return supported;
        }

        protected override MinerBase CreateMinerBase()
        {
            return new Excavator(PluginUUID);
        }

        public override IEnumerable<string> CheckBinaryPackageMissingFiles()
        {
            var pluginRootBinsPath = GetBinAndCwdPaths().Item2;
            return BinaryPackageMissingFilesCheckerHelpers.ReturnMissingFiles(pluginRootBinsPath, new List<string> { "excavator.exe" });
        }

        public override bool ShouldReBenchmarkAlgorithmOnDevice(BaseDevice device, Version benchmarkedPluginVersion, params AlgorithmType[] ids)
        {
            try
            {
                return benchmarkedPluginVersion.Major == 15 && benchmarkedPluginVersion.Minor < 6;
            }
            catch (Exception e)
            {
                Logger.Error("ExcavatorPlugin", $"ShouldReBenchmarkAlgorithmOnDevice {e}");
            }
            return false;
        }
    }
}
