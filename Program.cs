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

            var clientId = Environment.GetEnvironmentVariable("CLIENT_ID");
            var clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET");
            var tenantId = Environment.GetEnvironmentVariable("TENANT_ID");
            var subscriptionId = Environment.GetEnvironmentVariable("SUBSCRIPTION_ID");
            ClientSecretCredential credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
            ArmClient client = new ArmClient(credential, subscriptionId);

            string resourceGroupName = Utilities.CreateRandomName("ComputeSampleRG");
            string linuxVMName = Utilities.CreateRandomName("vm");
            string userName = Utilities.CreateUsername();
            string password = Utilities.CreatePassword();
            AzureLocation region = AzureLocation.EastUS;

            //=============================================================
            // MSI Authenticate

            AzureCredentials msiCredentails = new AzureCredentials(new MSILoginInformation(MSIResourceType.VirtualMachine)
            {
                UserAssignedIdentityClientId = clientId
            }, AzureEnvironment.AzureGlobalCloud);

            var azure = Azure
                .Configure()
                .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                .Authenticate(msiCredentails)
                .WithDefaultSubscription();

            Console.WriteLine("Selected subscription: " + azure.SubscriptionId);

            //=============================================================
            // Create a Linux VM using MSI credentials

            Console.WriteLine("Creating a Linux VM using MSI credentials");

            var virtualMachine = azure.VirtualMachines
                    .Define(linuxVMName)
                    .WithRegion(region)
                    .WithExistingResourceGroup(resourceGroupName)
                    .WithNewPrimaryNetwork("10.0.0.0/28")
                    .WithPrimaryPrivateIPAddressDynamic()
                    .WithoutPrimaryPublicIPAddress()
                    .WithPopularLinuxImage(KnownLinuxVirtualMachineImage.UbuntuServer16_04_Lts)
                    .WithRootUsername(userName)
                    .WithRootPassword(password)
                    .WithSize(VirtualMachineSizeTypes.Parse("Standard_D2a_v4"))
                    .Create();

            Console.WriteLine($"Created virtual machine using MSI credentials: {virtualMachine.Id}");
        }
    }
}
