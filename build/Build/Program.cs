// Builds and tests Typstium exactly as CI does; CI runs this build, so the two can't drift apart.
// See docs/specs/cake-sdk-build.md. Arguments: --target=<task> (Default), --configuration=<c> (Release), --coverage,
// --no-job-summary, --max-line-decrease=<pp> (0.5).
var target = Argument("target", "Default");

Setup(context =>
{
    var parameters = BuildParameters.Create(context, target);

    // A fresh coverage run starts empty; the CoverageReport target works on raw files someone put there.
    if (parameters.Coverage && parameters.Target != "CoverageReport")
    {
        EnsureDirectoryDoesNotExist(parameters.CoverageDirectory, new EnsureDirectoryDoesNotExistSettings { Recursive = true, Force = true });
    }

    return parameters;
});

RegisterBuildTasks();
RegisterTestTasks();
RegisterCoverageTasks();
RegisterDefaultTask();

RunTarget(target);
