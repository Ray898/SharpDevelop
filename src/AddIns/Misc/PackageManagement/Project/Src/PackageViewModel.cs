﻿// Copyright (c) 2014 AlphaSierraPapa for the SharpDevelop Team
//
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using ICSharpCode.PackageManagement;
using NuGet;
using NuGet.Resolver;
using PackageAction = NuGet.Resolver.PackageAction;

namespace ICSharpCode.PackageManagement
{
	public class PackageViewModel : ViewModelBase<PackageViewModel>
	{
		DelegateCommand addPackageCommand;
		DelegateCommand removePackageCommand;
		DelegateCommand managePackageCommand;
		
		PackageManagementSelectedProjects selectedProjects;
		IPackageManagementEvents packageManagementEvents;
		IPackageFromRepository package;
		IEnumerable<PackageAction> packageOperations = new PackageAction[0];
		PackageViewModelOperationLogger logger;
		readonly IPackageActionRunner actionRunner;
		IPackageViewModelParent parent;
		bool packageIsChanging;
		
		public PackageViewModel(
			IPackageViewModelParent parent,
			IPackageFromRepository package,
			PackageManagementSelectedProjects selectedProjects,
			IPackageManagementEvents packageManagementEvents,
			IPackageActionRunner actionRunner,
			ILogger logger)
		{
			this.parent = parent;
			this.package = package;
			this.selectedProjects = selectedProjects;
			this.packageManagementEvents = packageManagementEvents;
			this.actionRunner = actionRunner;
			this.logger = CreateLogger(logger);
			
			CreateCommands();
		}
		
		public IPackageViewModelParent GetParent()
		{
			return parent;
		}
		
		protected virtual PackageViewModelOperationLogger CreateLogger(ILogger logger)
		{
			return new PackageViewModelOperationLogger(logger, package);
		}
		
		void CreateCommands()
		{
			addPackageCommand = new DelegateCommand(param => AddOrManagePackage());
			removePackageCommand = new DelegateCommand(param => RemoveOrManagePackage());
			managePackageCommand = new DelegateCommand(param => ManagePackage());
		}
	
		public ICommand AddPackageCommand {
			get { return addPackageCommand; }
		}
		
		public ICommand RemovePackageCommand {
			get { return removePackageCommand; }
		}
		
		public ICommand ManagePackageCommand {
			get { return managePackageCommand; }
		}
		
		public IPackage GetPackage()
		{
			return package;
		}
		
		public bool HasLicenseUrl {
			get { return LicenseUrl != null; }
		}
		
		public Uri LicenseUrl {
			get { return package.LicenseUrl; }
		}
		
		public bool HasProjectUrl {
			get { return ProjectUrl != null; }
		}
		
		public Uri ProjectUrl {
			get { return package.ProjectUrl; }
		}
		
		public bool HasReportAbuseUrl {
			get { return ReportAbuseUrl != null; }
		}
		
		public Uri ReportAbuseUrl {
			get { return package.ReportAbuseUrl; }
		}
		
		public bool IsAdded {
			get {
				if (selectedProjects.HasSingleProjectSelected()) {
					return selectedProjects.GetSingleProjectSelected(package.Repository).IsPackageInstalled(package) ||
						(IsPackageInstalled() && !IsProjectPackage(package));
				}
				return IsPackageInstalled();
			}
		}
		
		protected bool IsPackageInstalled()
		{
			return IsPackageInstalled(package);
		}
		
		public IEnumerable<PackageDependencySet> Dependencies {
			get { return package.DependencySets; }
		}
		
		public bool HasDependencies {
			get { return package.HasDependencies; }
		}
		
		public bool HasNoDependencies {
			get { return !HasDependencies; }
		}
		
		public IEnumerable<string> Authors {
			get { return package.Authors; }
		}
		
		public bool HasDownloadCount {
			get { return package.DownloadCount >= 0; }
		}
		
		public string Id {
			get { return package.Id; }
		}
		
		public string Name {
			get { return package.GetName(); }
		}
		
		public bool HasGalleryUrl {
			get { return GalleryUrl != null; }
		}
		
		public bool HasNoGalleryUrl {
			get { return !HasGalleryUrl; }
		}
		
		public Uri GalleryUrl {
			get { return package.GalleryUrl; }
		}
		
		public Uri IconUrl {
			get { return package.IconUrl; }
		}
		
		public string Summary {
			get { return package.SummaryOrDescription(); }
		}
		
		public SemanticVersion Version {
			get { return package.Version; }
		}
		
		public int DownloadCount {
			get { return package.DownloadCount; }
		}
		
		public string Description {
			get { return package.Description; }
		}
		
		public DateTimeOffset? LastPublished {
			get { return package.Published; }
		}
		
		public bool HasLastPublished {
			get { return package.Published.HasValue; }
		}
		
		protected virtual bool IsProjectPackage(IPackage package)
		{
			return package.IsProjectPackage();
		}
		
		public void PackageChanged()
		{
			if (packageIsChanging)
				return;
 			
			packageIsChanging = true;
			
			OnPropertyChanged(model => model.IsManaged);
			OnPropertyChanged(model => model.IsAdded);
			
			packageIsChanging = false;
		}

		public void AddOrManagePackage()
		{
			if (selectedProjects.HasMultipleProjects()) {
				if (IsProjectPackage(package)) {
					ManagePackage();
				} else {
					AddPackage();
				}
			} else {
				AddPackage();
			}
		}

		public void AddPackage()
		{
			ClearReportedMessages();
			logger.LogAddingPackage();
			
			using (IDisposable operation = StartInstallOperation(package)) {
				if (IsProjectPackage(package)) {
					TryInstallingPackageIntoProject();
				} else {
					TryInstallingPackageIntoSolution();
				}
			}
			
			logger.LogAfterPackageOperationCompletes();
		}
		
		protected virtual IDisposable StartInstallOperation(IPackageFromRepository package)
		{
			return package.StartInstallOperation();
		}
		
		void ClearReportedMessages()
		{
			packageManagementEvents.OnPackageOperationsStarting();
		}
		
		void GetInstallOperationsForProject()
		{
			IPackageManagementProject project = GetSingleProjectSelected();
			project.Logger = logger;
			InstallPackageAction installAction = project.CreateInstallPackageAction();
			installAction.AllowPrereleaseVersions = parent.IncludePrerelease;
			packageOperations = project.GetInstallPackageOperations(package, installAction);
		}
		
		void GetInstallOperationsForSelectedProjects(IEnumerable<IPackageManagementSelectedProject> projects)
		{
			packageOperations = new PackageAction[0];
			
			IPackageManagementSelectedProject firstSelectedProject = projects.FirstOrDefault(project => project.IsSelected);
			
			if (firstSelectedProject != null) {
				InstallPackageAction installAction = firstSelectedProject.Project.CreateInstallPackageAction();
				installAction.AllowPrereleaseVersions = parent.IncludePrerelease;
				packageOperations = firstSelectedProject.Project.GetInstallPackageOperations(package, installAction);
			}
		}
		
		IPackageManagementProject GetSingleProjectSelected()
		{
			return selectedProjects.GetSingleProjectSelected(package.Repository);
		}
		
		IEnumerable<IPackage> GetPackagesRequiringLicenseAcceptance()
		{
			IList<IPackage> packagesToBeInstalled = GetPackagesToBeInstalled();
			return GetPackagesRequiringLicenseAcceptance(packagesToBeInstalled);
		}
		
		IEnumerable<IPackage> GetPackagesRequiringLicenseAcceptance(IEnumerable<IPackage> packagesToBeInstalled)
		{
			return packagesToBeInstalled.Where(PackageRequiresLicenseAcceptance);
		}
		
		IList<IPackage> GetPackagesToBeInstalled()
		{
			var packages = new List<IPackage>();
			foreach (PackageAction operation in packageOperations) {
				if (operation.ActionType == PackageActionType.Install) {
					packages.Add(operation.Package);
				}
			}
			return packages;
		}

		bool PackageRequiresLicenseAcceptance(IPackage packageToCheck)
		{
			return packageToCheck.RequireLicenseAcceptance && !IsPackageInstalled(packageToCheck);
		}
		
		bool IsPackageInstalled(IPackage packageToCheck)
		{
			return selectedProjects.Solution.IsPackageInstalled(packageToCheck);
		}

		protected virtual void TryInstallingPackageIntoSolution()
		{
			try {
				var solutionRepository = selectedProjects.Solution.CreateSolutionPackageRepository();
				var installAction = new InstallPackageAction(null, packageManagementEvents);
				installAction.Package = package;
				installAction.IgnoreDependencies = false;
				installAction.AllowPrereleaseVersions = parent.IncludePrerelease;
				
				var packageManager = new PackageManager(
					package.Repository,
					solutionRepository.PackagePathResolver,
					solutionRepository.FileSystem,
					solutionRepository.Repository);
				
				GetInstallOperationsForSolutionPackage(packageManager, solutionRepository, installAction, package);
				
				if (LicensesAccepted()) {
					InstallPackageIntoSolution(solutionRepository, installAction, package);
					packageManagementEvents.OnParentPackageInstalled(package);
				}
				
			} catch (Exception ex) {
				ReportError(ex);
				logger.LogError(ex);
			}
		}

		void GetInstallOperationsForSolutionPackage(
			IPackageManager packageManager,
			ISolutionPackageRepository solutionRepository, 
			InstallPackageAction installAction,
			IPackageFromRepository installPackage)
		{
			var resolver = new ActionResolver {
				Logger = logger,
				IgnoreDependencies = installAction.IgnoreDependencies,
				DependencyVersion = DependencyVersion.Lowest,
				AllowPrereleaseVersions = installAction.AllowPrereleaseVersions
			};
			var projectManager = new NullProjectManager(packageManager);
			resolver.AddOperation(NuGet.PackageAction.Install, package, projectManager);
//			var resolverFactory = new PackageOperationsResolverFactory();
//			var resolver = resolverFactory.CreateInstallPackageOperationResolver(
//				solutionRepository.Repository, 
//				installPackage.Repository, 
//				logger, 
//				installAction);
			packageOperations = resolver.ResolveActions();
		}

		void InstallPackageIntoSolution(
			ISolutionPackageRepository solutionRepository, 
			InstallPackageAction installAction,
			IPackageFromRepository installPackage)
		{
			var executor = new ActionExecutor {
				Logger = logger
			};
			executor.Execute(packageOperations);
		}

		protected virtual void TryUpdatingPackageInSolution()
		{
			try {
				var solutionRepository = selectedProjects.Solution.CreateSolutionPackageRepository();
				var updateAction = new UpdatePackageAction(null, packageManagementEvents);
				updateAction.Package = package;
				updateAction.AllowPrereleaseVersions = parent.IncludePrerelease;
				
				var packageManager = new PackageManager(
					package.Repository,
					solutionRepository.PackagePathResolver,
					solutionRepository.FileSystem,
					solutionRepository.Repository);

				GetUpdateOperationsForSolutionPackage(packageManager, solutionRepository, updateAction, package);
				
				if (LicensesAccepted()) {
					UpdateSolutionLevelPackage(solutionRepository, updateAction, package);
					
					IEnumerable<IPackage> updatedPackages = packageOperations
						.Where(packageOperation => packageOperation.Package != null)
						.Select(packageOperation => packageOperation.Package)
						.Distinct();
					
					packageManagementEvents.OnParentPackagesUpdated(updatedPackages);
				}
			} catch (Exception ex) {
				ReportError(ex);
				logger.LogError(ex);
			}
		}

		void GetUpdateOperationsForSolutionPackage(
			IPackageManager packageManager,
			ISolutionPackageRepository solutionRepository, 
			UpdatePackageAction updateAction,
			IPackageFromRepository updatePackage)
		{
//			var resolverFactory = new PackageOperationsResolverFactory();
//			var resolver = resolverFactory.CreateUpdatePackageOperationResolver(
//				solutionRepository.Repository, 
//				updatePackage.Repository, 
//				logger, 
//				updateAction);
			var resolver = new ActionResolver {
				Logger = logger,
				AllowPrereleaseVersions = updateAction.AllowPrereleaseVersions,
				IgnoreDependencies = !updateAction.UpdateDependencies
			};
			resolver.AddOperation(NuGet.PackageAction.Update, package, new NullProjectManager(packageManager));
			packageOperations = resolver.ResolveActions();
		}

		void UpdateSolutionLevelPackage(
			ISolutionPackageRepository solutionRepository,
			UpdatePackageAction updateAction,
			IPackageFromRepository updatePackage)
		{
			var executor = new ActionExecutor {
				Logger = logger
			};
			executor.Execute(packageOperations);
//			packageManager.UpdatePackage(
//				updatePackage.Id, 
//				updateAction.UpdateDependencies, 
//				updateAction.AllowPrereleaseVersions);
		}

		void TryInstallingPackageIntoProject()
		{
			try {
				GetInstallOperationsForProject();
				if (LicensesAccepted()) {
					InstallPackageIntoProject();
				}
			} catch (Exception ex) {
				ReportError(ex);
				logger.LogError(ex);
			}
		}

		void InstallPackageIntoProject()
		{
			InstallPackageIntoProject(packageOperations);
		}
		
		void InstallPackageIntoProject(IEnumerable<PackageAction> installOperations)
		{
			IPackageManagementProject project = GetSingleProjectSelected();
			ProcessPackageOperationsAction action = CreateInstallPackageAction(project);
			action.AllowPrereleaseVersions = parent.IncludePrerelease;
			action.Package = package;
			action.Operations = installOperations;
			actionRunner.Run(action);
		}
		
		protected virtual ProcessPackageOperationsAction CreateInstallPackageAction(
			IPackageManagementProject project)
		{
			return project.CreateInstallPackageAction();
		}
		
		void ReportError(Exception ex)
		{
			packageManagementEvents.OnPackageOperationError(ex);
		}
		
		public void RemoveOrManagePackage()
		{
			if (selectedProjects.HasMultipleProjects()) {
				if (IsProjectPackage(package)) {
					ManagePackage();
				} else {
					RemovePackage();
				}
			} else {
				RemovePackage();
			}
		}

		public void RemovePackage()
		{
			ClearReportedMessages();
			logger.LogRemovingPackage();

			if (IsProjectPackage(package)) {
				TryUninstallingPackageFromProject();
			} else {
				TrySolutionPackageUninstall();
			}

			logger.LogAfterPackageOperationCompletes();
		}
		
		void LogRemovingPackage()
		{
			logger.LogRemovingPackage();
		}

		void TrySolutionPackageUninstall()
		{
			TryUninstallingPackageFromSolution(package);
		}
		
		protected void TryUninstallingPackageFromSolution(IPackage removePackage)
		{
			try {
				var solutionPackageRepository = PackageManagementServices.Solution.CreateSolutionPackageRepository();
				var packageManager = new PackageManager(
					solutionPackageRepository.Repository,
					solutionPackageRepository.PackagePathResolver,
					solutionPackageRepository.FileSystem);
				
				var resolver = new ActionResolver {
					Logger = logger,
					DependencyVersion = DependencyVersion.Lowest,
					ForceRemove = false,
					RemoveDependencies = false
				};
				resolver.AddOperation(NuGet.PackageAction.Uninstall, removePackage, new NullProjectManager(packageManager));
				packageOperations = resolver.ResolveActions();
				var executor = new ActionExecutor {
					Logger = logger
				};
				executor.Execute(packageOperations);
//				packageManager.UninstallPackage(removePackage);
//				
//				solutionPackageRepository.Repository.RemovePackage(removePackage);
//				
				packageManagementEvents.OnParentPackageUninstalled(removePackage);
				
			} catch (Exception ex) {
				ReportError(ex);
				logger.LogError(ex);
			}
		}		

		void TryUninstallingPackageFromProject()
		{
			try {
				IPackageManagementProject project = GetSingleProjectSelected();
				UninstallPackageAction action = project.CreateUninstallPackageAction();
				action.Package = package;
				actionRunner.Run(action);
			} catch (Exception ex) {
				ReportError(ex);
				logger.LogError(ex);
			}
		}
		
		public bool IsManaged {
			get {
				if (selectedProjects.HasSingleProjectSelected()) {
					// Single Project selected 
					// Project-level Package Management
					return false; // [Add]/[Remove]
				}
				// Solution selected 
				// Project-level/Solution-level Package Management
				if (IsAdded) {
					if (IsProjectPackage(package)) {
						return true; // [Manage] Button
					}
					return false; // [Remove] Button
				}
				// package.IsProjectPackage() is too slow on uninstalled packages
				// check ([Add] or [Manage]) after user press on [Add] Package Button
				return false; // [Add] Button -> redirect to [Manage] if (package.IsProjectPackage())
			}
		}
		
		public void ManagePackage()
		{
			List<IPackageManagementSelectedProject> projects = GetSelectedProjectsForPackage();
			if (packageManagementEvents.OnSelectProjects(projects)) {
				ManagePackagesForSelectedProjects(projects);
			}
		}
		
		List<IPackageManagementSelectedProject> GetSelectedProjectsForPackage()
		{
			return selectedProjects.GetProjects(package).ToList();
		}
		
		public void ManagePackagesForSelectedProjects(IEnumerable<IPackageManagementSelectedProject> projects)
		{
			ClearReportedMessages();
			logger.LogManagingPackage();
			
			using (IDisposable operation = StartInstallOperation(package)) {
				TryManagePackagesForSelectedProjects(projects);
			}
			
			logger.LogAfterPackageOperationCompletes();
		}
		
		void TryManagePackagesForSelectedProjects(IEnumerable<IPackageManagementSelectedProject> projects)
		{
			try {
				if (IsPackageInstalled() || LicensesAccepted(projects)) {
					IList<ProcessPackageAction> actions = GetProcessPackageActionsForSelectedProjects(projects);
					RunActionsIfAnyExist(actions);
				}
			} catch (Exception ex) {
				ReportError(ex);
				logger.LogError(ex);
			}
		}
		
		protected virtual bool AnyProjectsSelected(IList<IPackageManagementSelectedProject> projects)
		{
			return projects.Any(project => project.IsSelected);
		}
		
		public IList<ProcessPackageAction> GetProcessPackageActionsForSelectedProjects(
			IEnumerable<IPackageManagementSelectedProject> selectedProjects)
		{
			var actions = new List<ProcessPackageAction>();
			foreach (IPackageManagementSelectedProject selectedProject in selectedProjects) {
				ProcessPackageAction action = CreatePackageManageAction(selectedProject);
				if (action != null) {
					actions.Add(action);
				}
			}
			return actions;
		}
		
		protected virtual ProcessPackageAction CreatePackageManageAction(IPackageManagementSelectedProject selectedProject)
		{
			if (selectedProject.IsSelected) {
				return CreateInstallPackageManageActionForSelectedProject(selectedProject);
			}
			return CreateUninstallPackageManageActionForSelectedProject(selectedProject);
		}
		
		ProcessPackageAction CreateInstallPackageManageActionForSelectedProject(IPackageManagementSelectedProject selectedProject)
		{
			if (!selectedProject.Project.IsPackageInstalled(package)) {
				return CreateInstallPackageAction(selectedProject);
			}
			return null;
		}
		
		ProcessPackageAction CreateUninstallPackageManageActionForSelectedProject(IPackageManagementSelectedProject selectedProject)
		{
			if (selectedProject.Project.IsPackageInstalled(package)) {
				return CreateUninstallPackageAction(selectedProject);
			}
			return null;
		}
		
		protected ProcessPackageAction CreateUpdatePackageManageActionForSelectedProject(IPackageManagementSelectedProject selectedProject)
		{
			if (!selectedProject.Project.IsPackageInstalled(package)) {
				return CreateUpdatePackageAction(selectedProject);
			}
			return null;
		}

		bool LicensesAccepted(IEnumerable<IPackageManagementSelectedProject> projects)
		{
			GetInstallOperationsForSelectedProjects(projects);
			return LicensesAccepted();
		}
		
		bool LicensesAccepted()
		{
			IEnumerable<IPackage> licensedPackages = GetPackagesRequiringLicenseAcceptance();
			if (licensedPackages.Any()) {
				return packageManagementEvents.OnAcceptLicenses(licensedPackages);
			}
			return true;
		}
		
		protected ProcessPackageAction CreateInstallPackageAction(IPackageManagementSelectedProject selectedProject)
		{
			IPackageManagementProject project = selectedProject.Project;
			project.Logger = logger;
			
			ProcessPackageOperationsAction action = CreateInstallPackageAction(project);
			action.AllowPrereleaseVersions = parent.IncludePrerelease;
			action.Package = package;
			return action;
		}
		
		protected ProcessPackageAction CreateUninstallPackageAction(IPackageManagementSelectedProject selectedProject)
		{
			IPackageManagementProject project = selectedProject.Project;
			project.Logger = logger;
			
			ProcessPackageAction action = project.CreateUninstallPackageAction();
			action.Package = package;
			return action;
		}
		
		protected ProcessPackageAction CreateUpdatePackageAction(IPackageManagementSelectedProject selectedProject)
		{
			IPackageManagementProject project = selectedProject.Project;
			project.Logger = logger;
			
			ProcessPackageOperationsAction action = project.CreateUpdatePackageAction();
			action.AllowPrereleaseVersions = parent.IncludePrerelease;
			action.Package = package;
			return action;
		}
		
		void RunActionsIfAnyExist(IList<ProcessPackageAction> actions)
		{
			if (actions.Any()) {
				actionRunner.Run(actions);
			}
		}
	}
}
