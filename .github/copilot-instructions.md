# Copilot Instructions

## Project Guidelines
- The project will use SignalR later for real-time features.
- Chat should be scoped: only between assigned professors and students, and between students within the same group.

## User Model Requirements
- In the User model, `StudentId` must be required (non-nullable), not optional. Remove the `?` from the property declaration.

## Task Management
- Users should be able to mark a task as completed or ready for review.
- Final status changes must be approved by the task creator (`CreatedById`) with oversight from a professor.
- Implement a progress bar UI layout on the Student Dashboard to display real project completion data (completed tasks vs total tasks) in Sprint 2.
- Professors should be able to monitor progress, tasks, and individual contribution activity to ensure accountability and support.

## Commit Message Guidelines
- Use conventional commits format for commit messages (e.g., `feat:`, `fix:`).
- Avoid using the word 'copilot' in commit messages for this repository; use neutral wording such as 'repository instructions' instead.
- Include detailed descriptions of what was added or changed in each commit.
- Preferred git commit author for this repository: ramankumaree202-collab <ramankumaree202@gmail.com>.

## Student Removal Workflow
- Lead removes student → Creates RemovalRequest → Professor approves/rejects.
- Student leaves group → Creates RemovalRequest → Professor approves/rejects.
- Professor removes student → Direct removal (no approval needed).
- Admin removes student → Direct removal (no approval needed).
- This ensures professor oversight and prevents casual group disruptions.