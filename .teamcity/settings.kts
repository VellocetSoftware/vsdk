@file:Suppress("ClassName")

import jetbrains.buildServer.configs.kotlin.*
import jetbrains.buildServer.configs.kotlin.buildSteps.script
import java.io.File

version = "2026.1"

private object Ci {
    private fun scriptResource(name: String): String {
        val scriptFile = File(DslContext.baseDir, "scripts/$name").takeIf { it.isFile }
            ?: error("Could not find TeamCity script resource: scripts/$name")

        return scriptFile.readText().trimEnd()
    }

    fun vsdkVcs(buildType: BuildType) {
        buildType.vcs {
            checkoutMode = CheckoutMode.AUTO
            root(DslContext.settingsRoot)
        }
    }

    fun linuxAutomationRequirements(buildType: BuildType) {
        buildType.requirements {
            matches("teamcity.agent.jvm.os.family", "Linux")
        }
    }

    fun linuxOrMacRequirements(buildType: BuildType) {
        buildType.requirements {
            matches("teamcity.agent.jvm.os.family", "Linux|Mac OS")
        }
    }

    fun teamCityDslValidateScript(): String = scriptResource("dsl-validate.sh")
}

project {
    buildTypesOrder = arrayListOf(
        Workflows_DslValidate,
        BuildLauncher
    )

    buildType(Workflows_DslValidate)
    buildType(BuildLauncher)
}

object Workflows_DslValidate : BuildType({
    name = "TeamCity DSL Validate"
    description = "Generates TeamCity Kotlin DSL to catch settings errors before TeamCity Cloud applies them."

    Ci.vsdkVcs(this)

    steps {
        script {
            name = "Generate TeamCity Configs"
            id = "Generate_TeamCity_Configs"
            scriptContent = Ci.teamCityDslValidateScript()
        }
    }

    Ci.linuxAutomationRequirements(this)

    failureConditions {
        executionTimeoutMin = 30
    }
})

object BuildLauncher : BuildType({
    id("BuildLauncher")
    name = "Build Launcher"
    description = "Builds the VSDK launcher artifact consumed by Grimwar SDK packaging."

    artifactRules = "VSDK/Build/Launcher/** => vsdk-launcher.zip"
    maxRunningBuilds = 1
    publishArtifacts = PublishMode.SUCCESSFUL

    Ci.vsdkVcs(this)

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

    outputParams {
        param("source.revision", "%build.vcs.number%")
        param("source.buildId", "%teamcity.build.id%")
        param("source.buildNumber", "%build.number%")
        param("source.buildTypeId", "%system.teamcity.buildType.id%")
        exposeAllParameters = false
    }

    Ci.linuxOrMacRequirements(this)

    failureConditions {
        executionTimeoutMin = 60
    }
})
