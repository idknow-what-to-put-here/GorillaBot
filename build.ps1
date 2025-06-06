<<<<<<< HEAD
function Reset-Templates{
    [cmdletbinding()]
    param(
        [string]$templateEngineUserDir = (join-path -Path $env:USERPROFILE -ChildPath .templateengine)
    )
    process{
        'resetting dotnet new templates. folder: "{0}"' -f $templateEngineUserDir | Write-host
        get-childitem -path $templateEngineUserDir -directory | Select-Object -ExpandProperty FullName | remove-item -recurse -force
        &dotnet new --debug:reinit
    }
}

$MyInvocation.MyCommand.Path | Split-Path | Push-Location # Run from this script's directory
Reset-Templates
nuget.exe pack .\GorillaTagModTemplate.nuspec -NoDefaultExcludes -OutputDirectory .\nupkg
dotnet new --install nupkg\*.nupkg
=======
function Reset-Templates{
    [cmdletbinding()]
    param(
        [string]$templateEngineUserDir = (join-path -Path $env:USERPROFILE -ChildPath .templateengine)
    )
    process{
        'resetting dotnet new templates. folder: "{0}"' -f $templateEngineUserDir | Write-host
        get-childitem -path $templateEngineUserDir -directory | Select-Object -ExpandProperty FullName | remove-item -recurse -force
        &dotnet new --debug:reinit
    }
}

$MyInvocation.MyCommand.Path | Split-Path | Push-Location # Run from this script's directory
Reset-Templates
nuget.exe pack .\GorillaTagModTemplate.nuspec -NoDefaultExcludes -OutputDirectory .\nupkg
dotnet new --install nupkg\*.nupkg
>>>>>>> 31ec9aed7aef5857e654107e2b42ea026d51c0de
Pop-Location