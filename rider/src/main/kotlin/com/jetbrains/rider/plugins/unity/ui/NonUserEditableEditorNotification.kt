package com.jetbrains.rider.plugins.unity.ui

import com.intellij.openapi.fileEditor.FileEditor
import com.intellij.openapi.project.DumbAware
import com.intellij.openapi.project.Project
import com.intellij.openapi.util.Key
import com.intellij.openapi.vfs.VirtualFile
import com.intellij.ui.EditorNotificationPanel
import com.intellij.ui.EditorNotifications
import com.jetbrains.rd.util.reactive.valueOrDefault
import com.jetbrains.rider.isUnityGeneratedProject
import com.jetbrains.rider.isUnityProject
import com.jetbrains.rider.model.EditorState
import com.jetbrains.rider.model.rdUnityModel
import com.jetbrains.rider.plugins.unity.util.isGeneratedUnityFile
import com.jetbrains.rider.plugins.unity.util.isNonEditableUnityFile
import com.jetbrains.rider.projectDir
import com.jetbrains.rider.projectView.solution
import java.io.File

class NonUserEditableEditorNotification : EditorNotifications.Provider<EditorNotificationPanel>(), DumbAware {

    companion object {
        val KEY = Key.create<EditorNotificationPanel>("non-user.editable.source.file.editing.notification.panel")
    }

    override fun getKey(): Key<EditorNotificationPanel> = KEY

    override fun createNotificationPanel(file: VirtualFile, fileEditor: FileEditor, project: Project): EditorNotificationPanel? {

        if (project.isUnityProject() && isNonEditableUnityFile(file)) {
            val panel = EditorNotificationPanel()
            panel.setText("This file is internal to Unity and should not be edited manually.")
            addShowInUnityAction(panel, file, project)
            return panel
        }

        if (project.isUnityGeneratedProject() && isGeneratedUnityFile(file)) {
            val panel = EditorNotificationPanel()
            panel.setText("This file is generated by Unity. Any changes made will be lost.")
            addShowInUnityAction(panel, file, project)
            return panel
        }

        return null
    }

    private fun addShowInUnityAction(panel: EditorNotificationPanel, file: VirtualFile, project: Project) {
        if (project.solution.rdUnityModel.editorState.valueOrDefault(EditorState.Disconnected) != EditorState.Disconnected) {
            panel.createActionLabel("Show in unity") {
                project.solution.rdUnityModel.showFileInUnity.fire(File(file.path).relativeTo(File(project.projectDir.path)).invariantSeparatorsPath)
            }
        }
    }
}

