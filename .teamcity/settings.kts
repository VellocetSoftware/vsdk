import jetbrains.buildServer.configs.kotlin.*
import jetbrains.buildServer.configs.kotlin.buildSteps.script

/*
The settings script is an entry point for defining a TeamCity
project hierarchy. The script should contain a single call to the
project() function with a Project instance or an init function as
an argument.

VcsRoots, BuildTypes, Templates, and subprojects can be
registered inside the project using the vcsRoot(), buildType(),
template(), and subProject() methods respectively.

To debug settings scripts in command-line, run the

    mvnDebug org.jetbrains.teamcity:teamcity-configs-maven-plugin:generate

command and attach your debugger to the port 8000.

To debug in IntelliJ Idea, open the 'Maven Projects' tool window (View
-> Tool Windows -> Maven Projects), find the generate task node
(Plugins -> teamcity-configs -> teamcity-configs:generate), the
'Debug' option is available in the context menu for the task.
*/

version = "2026.1"

project {
    buildTypesOrder = arrayListOf(BuildLauncher)
    buildType(BuildLauncher)
}

object BuildLauncher : BuildType({
    id("BuildLauncher")
    name = "Build Launcher"
    description = "Builds the VSDK launcher artifact consumed by Grimwar SDK packaging."

    artifactRules = "VSDK/Build/Launcher/** => vsdk-launcher.zip"
    maxRunningBuilds = 1
    publishArtifacts = PublishMode.SUCCESSFUL

    vcs {
        root(DslContext.settingsRoot)
    }

    steps {
        script {
            name = "Build Steam Tool Launcher"
            id = "Build_Steam_Tool_Launcher"
            scriptContent = """
                #!/usr/bin/env bash
                set -euo pipefail

                bash VSDK/scripts/build-steam-tool.sh
            """.trimIndent()
        }
    }

    requirements {
        matches("teamcity.agent.jvm.os.family", "Linux|Mac OS")
    }

    failureConditions {
        executionTimeoutMin = 60
    }
})
