using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.CodeAnalysis;
using Task = System.Threading.Tasks.Task;

namespace ResXQuickAdd.Services
{
    public class DesignerFileService
    {
        public async Task<bool> RegenerateDesignerFileAsync(string resxPath)
        {
            if (string.IsNullOrEmpty(resxPath) || !File.Exists(resxPath))
                return false;

            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                
                var designerPath = GetDesignerFilePath(resxPath);
                if (string.IsNullOrEmpty(designerPath))
                    return await TriggerCustomToolAsync(resxPath);

                return await TriggerCustomToolAsync(resxPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error regenerating designer file for {resxPath}: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TriggerCustomToolAsync(string resxPath)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var dte = await ServiceProvider.GetGlobalServiceAsync(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
                if (dte?.Solution == null)
                    return false;

                var projectItem = dte.Solution.FindProjectItem(resxPath);
                if (projectItem == null)
                    return false;

                if (!string.IsNullOrEmpty(projectItem.Properties?.Item("CustomTool")?.Value?.ToString()))
                {
                    var vsProjectItem = projectItem.Object as EnvDTE.ProjectItem;
                    vsProjectItem?.Properties?.Item("CustomTool")?.let(prop =>
                    {
                        var customTool = prop.Value?.ToString();
                        if (!string.IsNullOrEmpty(customTool))
                        {
                            projectItem.Properties.Item("CustomTool").Value = "";
                            projectItem.Properties.Item("CustomTool").Value = customTool;
                        }
                    });
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error triggering custom tool for {resxPath}: {ex.Message}");
                return false;
            }
        }

        private string GetDesignerFilePath(string resxPath)
        {
            var basePath = Path.ChangeExtension(resxPath, null);
            var designerPath = basePath + ".Designer.cs";
            
            return File.Exists(designerPath) ? designerPath : null;
        }

        public bool IsDesignerFileOutdated(string resxPath)
        {
            var designerPath = GetDesignerFilePath(resxPath);
            if (string.IsNullOrEmpty(designerPath))
                return true;

            try
            {
                var resxTime = File.GetLastWriteTime(resxPath);
                var designerTime = File.GetLastWriteTime(designerPath);
                
                return resxTime > designerTime;
            }
            catch
            {
                return true;
            }
        }

        public async Task<bool> InvalidateIntelliSenseAsync(Project project)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                if (project?.Solution?.Workspace == null)
                    return false;

                var workspace = project.Solution.Workspace;
                var solution = workspace.CurrentSolution;
                
                var updatedProject = solution.GetProject(project.Id);
                if (updatedProject != null)
                {
                    var newSolution = solution.RemoveProject(project.Id)
                        .AddProject(updatedProject.Id, updatedProject.Name, updatedProject.AssemblyName, updatedProject.Language);
                    
                    workspace.TryApplyChanges(newSolution);
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error invalidating IntelliSense: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> TriggerIncrementalBuildAsync(Project project)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var dte = await ServiceProvider.GetGlobalServiceAsync(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
                if (dte?.Solution == null)
                    return false;

                var vsProject = FindVsProject(dte, project.Name);
                if (vsProject != null)
                {
                    var solutionBuild = dte.Solution.SolutionBuild;
                    solutionBuild.BuildProject(solutionBuild.ActiveConfiguration.Name, vsProject.UniqueName, true);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error triggering incremental build: {ex.Message}");
                return false;
            }
        }

        private EnvDTE.Project FindVsProject(EnvDTE.DTE dte, string projectName)
        {
            try
            {
                foreach (EnvDTE.Project project in dte.Solution.Projects)
                {
                    if (string.Equals(project.Name, projectName, StringComparison.OrdinalIgnoreCase))
                        return project;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error finding VS project {projectName}: {ex.Message}");
            }

            return null;
        }

        public async Task NotifyFileChangedAsync(string filePath)
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var runningDocumentTable = await ServiceProvider.GetGlobalServiceAsync(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable;
                if (runningDocumentTable != null)
                {
                    runningDocumentTable.FindAndLockDocument(
                        (uint)_VSRDTFLAGS.RDT_NoLock,
                        filePath,
                        out IVsHierarchy hierarchy,
                        out uint itemId,
                        out IntPtr docData,
                        out uint cookie);

                    if (docData != IntPtr.Zero && hierarchy != null)
                    {
                        hierarchy.SetProperty(itemId, (int)__VSHPROPID.VSHPROPID_IsNonMemberItem, false);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error notifying file changed for {filePath}: {ex.Message}");
            }
        }
    }
}

static class Extensions
{
    public static void let<T>(this T item, Action<T> action)
    {
        if (item != null)
            action(item);
    }
}