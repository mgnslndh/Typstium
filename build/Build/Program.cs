var target = Argument("target", "Default");

Task("Default")
    .Does(() => Information("Cake.Sdk build scaffold"));

RunTarget(target);
