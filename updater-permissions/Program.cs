using NLog;
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
        private static Logger logger = LogManager.GetCurrentClassLogger();

        static void Main(string[] args)
        {
            if (args[0] != null)
            {
                string destinationDirectory = args[0];
                if (!Directory.Exists(destinationDirectory))
                {
                    try
                    {
                        Directory.CreateDirectory(destinationDirectory);
                    }catch(Exception ex)
                    {
                        Exception e = new Exception("Unable to create a directory at: " + destinationDirectory);
                        logger.Error(e);
                        throw e;
                    }
                    
                }
                //set to root directory first
                SetPermissions(destinationDirectory);
                //set inheritance to all other files
                SetPermissionsToAllFiles(destinationDirectory);
            }
            else
            {
                Environment.Exit(-1);
            }
            Environment.ExitCode = 1;
        }

        static void SetPermissions(string path)
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

            DirectoryInfo info = new DirectoryInfo(path);
            var security = info.GetAccessControl(AccessControlSections.Access);

            security.ModifyAccessRule(AccessControlModification.Set, accessRule, out bool result);

            if (!result)
            {
                //Console.Write("Failed to give full - control permission to all users for path " + path) ;
                //Console.Read();
                logger.Error("Permissions failed on: " + path);
                Environment.Exit(-2);
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
                //Console.Write("Failed to give full-control permission inheritance to all users for " + path);
                //Console.Read();
                logger.Error("Inheritance failed on: " + path);
                Environment.Exit(-3);
                throw new InvalidOperationException("Failed to give full-control permission inheritance to all users for " + path);
            }

            try
            {
                if (File.Exists(path))
                {
                    var fs = File.GetAccessControl(path);
                    fs.SetAccessRuleProtection(false, false);
                    File.SetAccessControl(path, fs);
                }
                security.SetAccessRuleProtection(false, false);
                info.SetAccessControl(security);
            }
            catch (Exception ex)
            {
                logger.Error("Cannot set ACL on: " + path + "   " + ex);
            }

        }

        static void SetPermissionsToAllFiles(string path)
        {
            //do directories first, or else you cant read for the files.
            foreach (string d in Directory.GetDirectories(path))
            {
                SetPermissions(d);
                SetPermissionsToAllFiles(d);
            }

            foreach (string f in Directory.GetFiles(path))
            {
                SetPermissions(f);
            }
        }
    }
}
