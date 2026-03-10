using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace APIPSI16.Models;

public partial class XcleratesystemslinksSampleDbContext : DbContext
{
    public XcleratesystemslinksSampleDbContext()
    {
    }

    public XcleratesystemslinksSampleDbContext(DbContextOptions<XcleratesystemslinksSampleDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AuditLog> AuditLogs { get; set; }

    public virtual DbSet<Chat> Chats { get; set; }

    public virtual DbSet<ChatMessage> ChatMessages { get; set; }

    public virtual DbSet<ChatUser> ChatUsers { get; set; }

    public virtual DbSet<Company> Companies { get; set; }

    public virtual DbSet<CompanyMember> CompanyMembers { get; set; }

    public virtual DbSet<Connection> Connections { get; set; }

    public virtual DbSet<EmployerCandidateHistory> EmployerCandidateHistories { get; set; }

    public virtual DbSet<InterviewRound> InterviewRounds { get; set; }

    public virtual DbSet<JobApplication> JobApplications { get; set; }

    public virtual DbSet<JobRole> JobRoles { get; set; }

    public virtual DbSet<Nationality> Nationalities { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<Opportunity> Opportunities { get; set; }

    public virtual DbSet<Post> Posts { get; set; }

    public virtual DbSet<PostComment> PostComments { get; set; }

    public virtual DbSet<PostReaction> PostReactions { get; set; }

    public virtual DbSet<ProfileEducation> ProfileEducations { get; set; }

    public virtual DbSet<ProfileExperience> ProfileExperiences { get; set; }

    public virtual DbSet<Skill> Skills { get; set; }

    public virtual DbSet<SkillEndorsement> SkillEndorsements { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserJobPreference> UserJobPreferences { get; set; }

    public virtual DbSet<UserSkill> UserSkills { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=sql.bsite.net\\MSSQL2016;Database=xcleratesystemslinks_SampleDB;User Id=xcleratesystemslinks_SampleDB;Password=XcelerateDB;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditLogId).HasName("PK__AuditLog__EB5F6CBDB08DE115");

            entity.ToTable("AuditLog");

            entity.Property(e => e.Action).HasMaxLength(100);
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.TargetType).HasMaxLength(50);

            entity.HasOne(d => d.User).WithMany(p => p.AuditLogs)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AuditLog_User");
        });

        modelBuilder.Entity<Chat>(entity =>
        {
            entity.HasKey(e => e.ChatId).HasName("PK__Chat__A9FBE7C651A747BD");

            entity.ToTable("Chat");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Type).HasMaxLength(50);

            entity.HasOne(d => d.CreatedByUser).WithMany(p => p.Chats)
                .HasForeignKey(d => d.CreatedByUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Chat__CreatedByU__32E0915F");
        });

        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(e => e.MessageId).HasName("PK__ChatMess__C87C0C9C7250148E");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Chat).WithMany(p => p.ChatMessages)
                .HasForeignKey(d => d.ChatId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ChatMessa__ChatI__3F466844");

            entity.HasOne(d => d.SenderUser).WithMany(p => p.ChatMessages)
                .HasForeignKey(d => d.SenderUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ChatMessa__Sende__403A8C7D");
        });

        modelBuilder.Entity<ChatUser>(entity =>
        {
            entity.HasKey(e => e.ChatUserId).HasName("PK__ChatUser__BFA9F7900B8CB40A");

            entity.HasIndex(e => new { e.ChatId, e.UserId }, "UQ_Chat_User").IsUnique();

            entity.Property(e => e.JoinedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Role)
                .HasMaxLength(50)
                .HasDefaultValue("member");

            entity.HasOne(d => d.Chat).WithMany(p => p.ChatUsers)
                .HasForeignKey(d => d.ChatId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ChatUsers__ChatI__38996AB5");

            entity.HasOne(d => d.User).WithMany(p => p.ChatUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ChatUsers__UserI__398D8EEE");
        });

        modelBuilder.Entity<Company>(entity =>
        {
            entity.HasKey(e => e.CompanyId).HasName("PK__Companie__2D971CAC7600FB81");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Industry).HasMaxLength(100);
            entity.Property(e => e.Location).HasMaxLength(150);
            entity.Property(e => e.Name).HasMaxLength(200);
        });

        modelBuilder.Entity<CompanyMember>(entity =>
        {
            entity.HasKey(e => e.CompanyMemberId).HasName("PK__CompanyM__F1920989987E4A54");

            entity.HasIndex(e => new { e.CompanyId, e.UserId }, "UQ_Company_User").IsUnique();

            entity.Property(e => e.Title).HasMaxLength(100);

            entity.HasOne(d => d.Company).WithMany(p => p.CompanyMembers)
                .HasForeignKey(d => d.CompanyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CompanyMembers_Company");

            entity.HasOne(d => d.User).WithMany(p => p.CompanyMembers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_CompanyMembers_User");
        });

        modelBuilder.Entity<Connection>(entity =>
        {
            entity.HasKey(e => e.ConnectionId).HasName("PK__Connecti__404A64931F69BCCF");

            entity.HasIndex(e => new { e.RequesterUserId, e.AddresseeUserId }, "UQ__Connecti__D964BF246B6D1B80").IsUnique();

            entity.Property(e => e.AcceptedAt).HasColumnType("datetime");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
        });

        modelBuilder.Entity<EmployerCandidateHistory>(entity =>
        {
            entity.HasKey(e => e.EmployerCandidateHistoryId).HasName("PK__Employer__7ED6A363F8F4F90F");

            entity.ToTable("EmployerCandidateHistory");

            entity.HasIndex(e => new { e.CompanyId, e.LastContactAt }, "IX_ECH_Company_LastContact").IsDescending(false, true);

            entity.HasIndex(e => new { e.CompanyId, e.UserId }, "IX_ECH_Company_User");

            entity.HasIndex(e => e.UserId, "IX_ECH_User");

            entity.HasIndex(e => new { e.CompanyId, e.UserId, e.OpportunityId }, "UQ_EmployerCandidateHistory").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.LastContactAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Outcome).HasMaxLength(50);
            entity.Property(e => e.StageReached).HasMaxLength(50);

            entity.HasOne(d => d.Company).WithMany(p => p.EmployerCandidateHistories)
                .HasForeignKey(d => d.CompanyId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__EmployerC__Compa__72C60C4A");

            entity.HasOne(d => d.Opportunity).WithMany(p => p.EmployerCandidateHistories)
                .HasForeignKey(d => d.OpportunityId)
                .HasConstraintName("FK__EmployerC__Oppor__74AE54BC");

            entity.HasOne(d => d.User).WithMany(p => p.EmployerCandidateHistories)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__EmployerC__UserI__73BA3083");
        });

        modelBuilder.Entity<InterviewRound>(entity =>
        {
            entity.HasKey(e => e.InterviewRoundId).HasName("PK__Intervie__33D9F1FB804C69B8");

            entity.Property(e => e.ScheduledAt).HasColumnType("datetime");

            entity.HasOne(d => d.InterviewerUser).WithMany(p => p.InterviewRounds)
                .HasForeignKey(d => d.InterviewerUserId)
                .HasConstraintName("FK__Interview__Inter__43D61337");

            entity.HasOne(d => d.JobApplication).WithMany(p => p.InterviewRounds)
                .HasForeignKey(d => d.JobApplicationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Interview__JobAp__42E1EEFE");
        });

        modelBuilder.Entity<JobApplication>(entity =>
        {
            entity.HasKey(e => e.JobApplicationId).HasName("PK__JobAppli__BD557F85702879F0");

            entity.Property(e => e.AppliedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Name)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");

            entity.HasOne(d => d.Opportunity).WithMany(p => p.JobApplications)
                .HasForeignKey(d => d.OpportunityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__JobApplic__Oppor__3F115E1A");

            entity.HasOne(d => d.User).WithMany(p => p.JobApplications)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__JobApplic__UserI__40058253");
        });

        modelBuilder.Entity<JobRole>(entity =>
        {
            entity.HasKey(e => e.JobRoleId).HasName("PK__JobRoles__6D8BAC2F3B334D72");

            entity.HasIndex(e => e.Name, "UQ__JobRoles__737584F68DB4257F").IsUnique();

            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<Nationality>(entity =>
        {
            entity.HasKey(e => e.NationalityId).HasName("PK__National__F628E744043E379B");

            entity.HasIndex(e => e.Name, "UQ__National__737584F6FEC464F0").IsUnique();

            entity.Property(e => e.Isocode)
                .HasMaxLength(3)
                .IsUnicode(false)
                .IsFixedLength()
                .HasColumnName("ISOCode");
            entity.Property(e => e.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.NotificationId).HasName("PK__Notifica__20CF2E122B839BE3");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Type).HasMaxLength(100);

            entity.HasOne(d => d.ActorUser).WithMany(p => p.NotificationActorUsers)
                .HasForeignKey(d => d.ActorUserId)
                .HasConstraintName("FK__Notificat__Actor__3A4CA8FD");

            entity.HasOne(d => d.User).WithMany(p => p.NotificationUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Notificat__UserI__395884C4");
        });

        modelBuilder.Entity<Opportunity>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Oportuni__3214EC274DEA57B2");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.CompanyId).HasColumnName("CompanyID");
            entity.Property(e => e.CreatorId).HasColumnName("CreatorID");
            entity.Property(e => e.Location).HasMaxLength(200);
            entity.Property(e => e.Title).HasMaxLength(100);

            entity.HasOne(d => d.Company).WithMany(p => p.Opportunities)
                .HasForeignKey(d => d.CompanyId)
                .HasConstraintName("FK_Opportunities_Company");

            entity.HasOne(d => d.Creator).WithMany(p => p.Opportunities)
                .HasForeignKey(d => d.CreatorId)
                .HasConstraintName("FK_Opportunities_CreatedBy");
        });

        modelBuilder.Entity<Post>(entity =>
        {
            entity.HasKey(e => e.PostId).HasName("PK__Posts__AA1260184B9F8F56");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime");

            entity.HasOne(d => d.User).WithMany(p => p.Posts)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Posts__UserId__29221CFB");
        });

        modelBuilder.Entity<PostComment>(entity =>
        {
            entity.HasKey(e => e.CommentId).HasName("PK__PostComm__C3B4DFCA72C8D2D9");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.ParentComment).WithMany(p => p.InverseParentComment)
                .HasForeignKey(d => d.ParentCommentId)
                .HasConstraintName("FK__PostComme__Paren__2EDAF651");

            entity.HasOne(d => d.Post).WithMany(p => p.PostComments)
                .HasForeignKey(d => d.PostId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PostComme__PostI__2CF2ADDF");

            entity.HasOne(d => d.User).WithMany(p => p.PostComments)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PostComme__UserI__2DE6D218");
        });

        modelBuilder.Entity<PostReaction>(entity =>
        {
            entity.HasKey(e => e.ReactionId).HasName("PK__PostReac__46DDF9B4004EE135");

            entity.HasIndex(e => new { e.PostId, e.UserId, e.ReactionType }, "UX_Post_User_Reaction").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Post).WithMany(p => p.PostReactions)
                .HasForeignKey(d => d.PostId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PostReact__PostI__339FAB6E");

            entity.HasOne(d => d.User).WithMany(p => p.PostReactions)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__PostReact__UserI__3493CFA7");
        });

        modelBuilder.Entity<ProfileEducation>(entity =>
        {
            entity.HasKey(e => e.EducationId).HasName("PK__ProfileE__4BBE380527A7D966");

            entity.Property(e => e.Degree).HasMaxLength(200);
            entity.Property(e => e.FieldOfStudy).HasMaxLength(200);
            entity.Property(e => e.School).HasMaxLength(200);

            entity.HasOne(d => d.User).WithMany(p => p.ProfileEducations)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ProfileEd__UserI__151B244E");
        });

        modelBuilder.Entity<ProfileExperience>(entity =>
        {
            entity.HasKey(e => e.ExperienceId).HasName("PK__ProfileE__2F4E34491AA78603");

            entity.Property(e => e.CompanyName).HasMaxLength(200);
            entity.Property(e => e.Location).HasMaxLength(150);
            entity.Property(e => e.Title).HasMaxLength(150);

            entity.HasOne(d => d.User).WithMany(p => p.ProfileExperiences)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__ProfileEx__UserI__123EB7A3");
        });

        modelBuilder.Entity<Skill>(entity =>
        {
            entity.HasKey(e => e.SkillId).HasName("PK__Skills__DFA0918781313143");

            entity.HasIndex(e => e.Name, "UQ__Skills__737584F61738EFED").IsUnique();

            entity.Property(e => e.Name).HasMaxLength(150);
        });

        modelBuilder.Entity<SkillEndorsement>(entity =>
        {
            entity.HasKey(e => e.EndorsementId).HasName("PK__SkillEnd__DBB8336F15D1AEF4");

            entity.HasIndex(e => new { e.UserSkillId, e.EndorserUserId }, "UX_Endorse").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.EndorserUser).WithMany(p => p.SkillEndorsements)
                .HasForeignKey(d => d.EndorserUserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__SkillEndo__Endor__245D67DE");

            entity.HasOne(d => d.UserSkill).WithMany(p => p.SkillEndorsements)
                .HasForeignKey(d => d.UserSkillId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__SkillEndo__UserS__236943A5");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CCACFE71B925");

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.PasswordHash).HasMaxLength(200);
            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.ProfileBio)
                .HasMaxLength(250)
                .IsUnicode(false)
                .HasColumnName("Profile_Bio");
        });

        modelBuilder.Entity<UserJobPreference>(entity =>
        {
            entity.HasKey(e => e.UserJobPreferenceId).HasName("PK__UserJobP__F82C54A77C80E128");

            entity.HasIndex(e => new { e.UserId, e.JobRoleId }, "UQ_User_JobRole").IsUnique();

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.JobRole).WithMany(p => p.UserJobPreferences)
                .HasForeignKey(d => d.JobRoleId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__UserJobPr__JobRo__02084FDA");
        });

        modelBuilder.Entity<UserSkill>(entity =>
        {
            entity.HasKey(e => e.UserSkillId).HasName("PK__UserSkil__2F28BE56D1484E42");

            entity.HasIndex(e => new { e.UserId, e.SkillId }, "UX_User_Skill").IsUnique();

            entity.Property(e => e.AddedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Skill).WithMany(p => p.UserSkills)
                .HasForeignKey(d => d.SkillId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__UserSkill__Skill__1EA48E88");

            entity.HasOne(d => d.User).WithMany(p => p.UserSkills)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__UserSkill__UserI__1DB06A4F");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
