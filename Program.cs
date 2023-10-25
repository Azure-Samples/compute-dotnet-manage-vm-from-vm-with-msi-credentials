// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager.Resources.Models;
using Azure.ResourceManager.Samples.Common;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager;
using System.Net;
using Azure.ResourceManager.Network.Models;
using Azure.ResourceManager.Network;
using Azure.ResourceManager.Compute.Models;
using Azure.ResourceManager.Compute;
using System.Net.NetworkInformation;
using System.Xml.Linq;
using Azure.Core.Pipeline;

namespace ManageVirtualMachineFromMSIEnabledVirtualMachine
{
    public class Program
    {
        /**
         * Azure Compute sample for managing virtual machine from Managed Service Identity (MSI) enabled virtual machine -
         *   - Create a virtual machine using MSI credentials from System assigned or User Assigned MSI enabled VM.
         */
        public static void Main(string[] args)
        {
            // This sample required to be run from a MSI (User Assigned or System Assigned) enabled virtual machine with role
            // based contributor access to the resource group specified as the second command line argument.
            //
            // see https://github.com/Azure-Samples/compute-dotnet-manage-user-assigned-msi-enabled-virtual-machine.git
            //

            string usage = "Usage: dotnet run <subscription-id> <rg-name> [<client-id>]";
            if (args.Length < 2)
            {
                throw new ArgumentException(usage);
            }

            string subscriptionId = args[0];
            string resourceGroupName = args[1];
            string clientId = args.Length > 2 ? args[2] : null;
            string linuxVMName = Utilities.CreateRandomName("vm");
            string userName = Utilities.CreateUsername();
            string password = Utilities.CreatePassword();



            //=============================================================
            // MSI Authenticate

            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ManagedIdentityClientId = clientId
            });

            ArmClient client = new ArmClient(credential, subscriptionId);
            SubscriptionResource subscription = client.GetDefaultSubscription();
            var resourceGroupLro = subscription.GetResourceGroups().Get(resourceGroupName);
            ResourceGroupResource resourceGroup = resourceGroupLro.Value;

            Console.WriteLine("Selected subscription: " + subscription.Data.Id);

            //=============================================================
            // Create a Linux VM using MSI credentials

            Console.WriteLine("Creating a Linux VM using MSI credentials");

            Utilities.Log("Pre-creating some resources that the VM depends on");

            // Creating a virtual network
            Utilities.Log("Creating virtual network...");
            string vnetName = Utilities.CreateRandomName("vnet");
            VirtualNetworkData vnetInput = new VirtualNetworkData()
            {
                Location = resourceGroup.Data.Location,
                AddressPrefixes = { "10.10.0.0/16" },
                Subnets =
                    {
                        new SubnetData() { Name = "subnet1", AddressPrefix = "10.10.1.0/24"},
                        new SubnetData() { Name = "subnet2", AddressPrefix = "10.10.2.0/24"},
                    },
            };
            var vnetLro = resourceGroup.GetVirtualNetworks().CreateOrUpdate(WaitUntil.Completed, vnetName, vnetInput);
            Utilities.Log($"Created a virtual network: {vnetLro.Value.Data.Name}");

            // Creating network interface
            Utilities.Log($"Creating network interface...");
            string nicName = Utilities.CreateRandomName("nic");
            var nicInput = new NetworkInterfaceData()
            {
                Location = resourceGroup.Data.Location,
                IPConfigurations =
                    {
                        new NetworkInterfaceIPConfigurationData()
                        {
                            Name = "default-config",
                            PrivateIPAllocationMethod = NetworkIPAllocationMethod.Dynamic,
                            Subnet = new SubnetData()
                            {
                                Id = vnetLro.Value.Data.Subnets[0].Id
                            },
                        }
                    }
            };
            var networkInterfaceLro = resourceGroup.GetNetworkInterfaces().CreateOrUpdate(WaitUntil.Completed, nicName, nicInput);
            Utilities.Log($"Created network interface: {networkInterfaceLro.Value.Data.Name}");

            Utilities.Log("Creating a Linux VM with MSI associated and install DotNet and Git");

            VirtualMachineData linuxVMInput = new VirtualMachineData(resourceGroup.Data.Location)
            {
                HardwareProfile = new VirtualMachineHardwareProfile()
                {
                    VmSize = VirtualMachineSizeType.StandardF2
                },
                StorageProfile = new VirtualMachineStorageProfile()
                {
                    ImageReference = new ImageReference()
                    {
                        Publisher = "Canonical",
                        Offer = "UbuntuServer",
                        Sku = "16.04-LTS",
                        Version = "latest",
                    },
                    OSDisk = new VirtualMachineOSDisk(DiskCreateOptionType.FromImage)
                    {
                        OSType = SupportedOperatingSystemType.Linux,
                        Caching = CachingType.ReadWrite,
                        ManagedDisk = new VirtualMachineManagedDisk()
                        {
                            StorageAccountType = StorageAccountType.StandardLrs
                        }
                    },
                },
                OSProfile = new VirtualMachineOSProfile()
                {
                    AdminUsername = userName,
                    AdminPassword = password,
                    ComputerName = linuxVMName,
                },
                NetworkProfile = new VirtualMachineNetworkProfile()
                {
                    NetworkInterfaces =
                        {
                            new VirtualMachineNetworkInterfaceReference()
                            {
                                Id = networkInterfaceLro.Value.Data.Id,
                                Primary = true,
                            }
                        }
                },
            };
            var linuxVmLro = resourceGroup.GetVirtualMachines().CreateOrUpdate(WaitUntil.Completed, linuxVMName, linuxVMInput);

            Console.WriteLine($"Created virtual machine {linuxVmLro.Value.Data.Name} using MSI credentials: ");
        }
    }
}
