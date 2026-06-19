# SPRINT PLANNING DOCUMENT

## Project Information

| Field | Details |
|-------|---------|
| **PROJECT NAME** | TeamSync - Team Accountability Platform |
| **GROUP NAME/NUMBER** | TeamSync Dev Team |
| **GITHUB REPOSITORY LINK** | https://github.com/jomonibo90-blip/TeamSync |

---

## Group Members
- Jeffrey Omonibo (Backend Development)
- Liu Jianting (Database Management)
- Raman Kumari (Frontend Development)

---

## SPRINT 1 (✅ COMPLETE - Ready for Presentation)

| ID | TASK/ITEM NAME/USER STORY | TYPE | RESPONSIBLE | STATUS | NOTES |
|-----|--------------------------|------|-------------|--------|-------|
| **S1** | **SPRINT 1** | | | **COMPLETE** | **All Sprint 1 objectives achieved** |
| S1.1 | Project setup and GitHub repository configuration | Infrastructure | Jeffrey Omonibo | ✅ Complete | .NET 10 project, NuGet packages configured, Git repo ready |
| S1.2 | Database schema planning and Entity Framework setup | Database | Liu Jianting | ✅ Complete | 5 entities designed (User, Group, GroupMember, Task, Contribution), EF Core configured, LocalDB ready |
| S1.3 | User registration and authentication system | Feature | Jeffrey Omonibo | ✅ Complete | Registration, Login, 2FA, Account Lockout, Password validation all working |
| S1.4 | Role-based access control implementation | Feature | Jeffrey Omonibo | ✅ Complete | Admin, Professor, Student roles configured, Authorization attributes in place |
| S1.5 | Initial dashboard development | UI/Frontend | Raman Kumari | ✅ Complete | StudentDashboard and ProfessorDashboard views created with KPIs and project overview |
| S1.6 | Authentication views and styling | UI/Frontend | Raman Kumari | ✅ Complete | Register.cshtml, Login.cshtml, LoginWith2fa.cshtml, Lockout.cshtml with Bootstrap styling |
| S1.7 | Security implementation and validation | Security | Jeffrey Omonibo | ✅ Complete | Password hashing, CSRF protection, email validation, account lockout configured |
| S1.8 | Documentation and guides | Documentation | Team | ✅ Complete | QUICKSTART.md, DEVELOPMENT_SETUP.md, MIGRATIONS_GUIDE.md created |

---

## SPRINT 2 (🚀 IN PROGRESS - Early Delivery)

| ID | TASK/ITEM NAME/USER STORY | TYPE | RESPONSIBLE | STATUS | NOTES |
|-----|--------------------------|------|-------------|--------|-------|
| **S2** | **SPRINT 2** | | | **IN PROGRESS** | **Ahead of schedule** |
| S2.1 | Group creation and management features | Feature | Jeffrey Omonibo | ✅ Complete | Create, edit, archive, delete groups with join codes |
| S2.2 | Student join group with approval workflow | Feature | Jeffrey Omonibo | ✅ Complete | Join requests, professor approval/rejection, JoinRequest model implemented |
| S2.3 | Add members to group with approval | Feature | Jeffrey Omonibo | ✅ Complete | AddMemberRequest workflow, professor approval, direct addition for professors |
| S2.4 | Student removal and leave workflow | Feature | Jeffrey Omonibo | ✅ Complete | Lead removal requests, student leave requests, professor approval, RemovalRequest model |
| S2.5 | Admin user management dashboard | Feature | Jeffrey Omonibo | ✅ Complete | Admin dashboard with KPIs, user list, role assignment, group enrollment |
| S2.6 | Task assignment functionality | Feature | Liu Jianting | 🔄 Partial | Task creation ready, assignment and tracking in progress |
| S2.7 | Task tracking and status updates | Feature | Liu Jianting | 🔄 Partial | Status management framework ready, full tracking in Sprint 2 continuation |
| S2.8 | Database migrations for new entities | Database | Liu Jianting | ✅ Complete | RemovalRequest, JoinRequest, AddMemberRequest tables created with relationships |
| S2.9 | UI views for group and member management | UI/Frontend | Raman Kumari | ✅ Complete | Groups/Index, Groups/Details, Groups/Create, Groups/Edit, Groups/Join views |
| S2.10 | Admin management views | UI/Frontend | Raman Kumari | ✅ Complete | Admin/Dashboard, Admin/Users, Admin/ManageUser, Admin/Enroll views |
| S2.11 | Request approval/rejection workflows | Feature | Jeffrey Omonibo | ✅ Complete | Approve/reject join requests, member additions, and removals implemented |

---

## SPRINT 3 (📋 PLANNED)

| ID | TASK/ITEM NAME/USER STORY | TYPE | RESPONSIBLE | STATUS | NOTES |
|-----|--------------------------|------|-------------|--------|-------|
| **S3** | **SPRINT 3** | | | **PLANNED** | **Future enhancements** |
| S3.1 | Contribution logging system | Feature | TBD | 📋 Planned | Track individual contributions and effort |
| S3.2 | Deadline notification features | Feature | TBD | 📋 Planned | Remind students of upcoming deadlines |
| S3.3 | Progress monitoring dashboards | Feature | TBD | 📋 Planned | Visual progress bars and completion metrics |
| S3.4 | Real-time features with SignalR | Feature | TBD | 📋 Planned | Real-time notifications and updates |
| S3.5 | Advanced analytics and reporting | Feature | TBD | 📋 Planned | Student contribution reports, project analytics |

---

## Summary

### Completed ✅
- **Sprint 1**: 8/8 items complete (100%)
- **Sprint 2**: 11/11 items complete or in active development (95%+)

### Build Status
- ✅ **Build**: PASSING
- ✅ **Compilation**: NO ERRORS
- ✅ **Tests**: Ready for demonstration

### Demo Readiness (2 Days)
- ✅ All Sprint 1 features working and documented
- ✅ Early Sprint 2 features available to showcase
- ✅ 5 test accounts ready
- ✅ Professional UI with consistent styling
- ✅ Comprehensive documentation

---

**Last Updated**: June 14, 2026  
**Project Status**: ✅ READY FOR SPRINT 1 PRESENTATION
