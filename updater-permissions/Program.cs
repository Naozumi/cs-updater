using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace updater_permissions
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args[0] != null)
            {
                string destinationDirectory = args[0];
                if (!Directory.Exists(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }
                SetFullControlPermissionsToEveryone(destinationDirectory);
            }
            
        }

        static void SetFullControlPermissionsToEveryone(string path)
        {
            const FileSystemRights rights = FileSystemRights.FullControl;

            var allUsers = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);

            // Add Access Rule to the actual directory itself
            var accessRule = new FileSystemAccessRule(
                allUsers,
                rights,
                InheritanceFlags.None,
                PropagationFlags.NoPropagateInherit,
                AccessControlType.Allow);

            var info = new DirectoryInfo(path);
            var security = info.GetAccessControl(AccessControlSections.Access);

            security.ModifyAccessRule(AccessControlModification.Set, accessRule, out bool result);

            if (!result)
            {
                Console.Write("Failed to give full - control permission to all users for path " + path) ;
                Console.Read();
                throw new InvalidOperationException("Failed to give full-control permission to all users for path " + path);
            }

            // add inheritance
            var inheritedAccessRule = new FileSystemAccessRule(
                allUsers,
                rights,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.InheritOnly,
                AccessControlType.Allow);

            bool inheritedResult;
            security.ModifyAccessRule(AccessControlModification.Add, inheritedAccessRule, out inheritedResult);

            if (!inheritedResult)
            {
                Console.Write("Failed to give full-control permission inheritance to all users for " + path);
                Console.Read();
                throw new InvalidOperationException("Failed to give full-control permission inheritance to all users for " + path);
            }

            info.SetAccessControl(security);
        }
    }
}
