# Copilot Instructions

## Project Guidelines
- The project will use SignalR later for real-time features.
- Chat should be scoped: only between assigned professors and students, and between students within the same group.

## User Model Requirements
- In the User model, `StudentId` must be required (non-nullable), not optional. Remove the `?` from the property declaration.

## Task Management
- TeamSync is an accountability and tracking system for educational team projects, not an execution environment. The actual development work happens outside TeamSync in real repositories/projects. TeamSync records and tracks:
  - Task assignments and progress
  - Student contributions and individual accountability
  - Task status changes (Pending, In Progress, Completed, Ready for Review)
  - Professor oversight of individual contribution activity
  - Audit trails of completed work
- Users should be able to mark a task as completed or ready for review.
- Final status changes must be approved by the task creator (`CreatedById`) with oversight from a professor.
- Implement a progress bar UI layout on the Student Dashboard to display real project completion data (completed tasks vs total tasks) in Sprint 2.
- Professors should be able to monitor progress, tasks, and individual contribution activity to ensure accountability and support.

## Commit Message Guidelines
- Use conventional commits format for commit messages (e.g., `feat:`, `fix:`).
- Include detailed descriptions of what was added or changed in each commit.

## Student Removal Workflow
- Lead removes student → Creates RemovalRequest → Professor approves/rejects.
- Student leaves group → Creates RemovalRequest → Professor approves/rejects.
- Professor removes student → Direct removal (no approval needed).
- Admin removes student → Direct removal (no approval needed).
- This ensures professor oversight and prevents casual group disruptions.