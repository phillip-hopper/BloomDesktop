On Windows, Bloom Desktop uses Microsoft's open source ["WIX Toolset"](http://wixtoolset.org/) installer maker.

Requirements for building the installer:

-- bash so you can run build/getDependencies.sh

- WIX Toolset. Version 1.0 shipped with WIX Toolset 3.7.

- MSBuild Community tasks. I got the msi from https://github.com/loresoft/msbuildtasks/downloads.

- Palaso.BuildTasks.dll must be in your build directory. I found one in lib/dotnet and copied it. You may need to download it if you haven't already.

- The ZIP file of [XulRunner](http://ftp.mozilla.org/pub/mozilla.org/xulrunner/releases/) that build/bloom.proj calls for. At Version 1.0, this is xulrunner-8.0.en-US.win32.zip.

- build/ChorusInstallerStuff/ChorusMergeModule.msm. I got one from build.palaso.org, one of the artifacts of the "chorus-win32-master Continuous" branch. Probably a more appropriate branch to use would be chorus-win32-Bloom1.0, but this does not currently have the artifact. Maybe it will by the time you read this.

- A customized .bat for running the installer:
Copy build/testInstallerBuild.bat, rename it something like 'RalphsTestInstallerBuild.bat'. Then modify the location of the

Run your bat file, and if all goes well you'll get an installer in your output/installer folder.

If you have problems, in addition to reading the logs, you can open src/installer/installer.wixproj and read it, it may give you some hints about what is expected.

Note: there are currently *many* WIX warnings in a successful build, it's not something you did wrong. (Contribution Welcome: get rid of those warnings by updating the wix file to wix's current expectations.)

Note: currently the process of making an installer replaces the "Property_ProductVersion" placeholder in installer.wxs with a real value. This change must never be committed to version control, or users will suffer. (Contribution Welcome: make it operate on a _copy_ of the that file so we don't have to worry about it.)

###A note about WIX and the Palaso TeamCity Installation
Different projects may expect different versions of WIX, but it appears they can't be installed side-by-side (e.g. 3.5 and 3.7). Therefore we have a windows "junction" on the TeamCity agents that points projects looking for 3.5 to 3.7. Over time the specific versions involved might change, but knowing this may help you make sense of what you see on TeamCity.