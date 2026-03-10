using System;
using System.Collections.Generic;

namespace APIPSI16.Models;

public partial class User
{
    public int UserId { get; set; }

    public string? Name { get; set; }

    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }

    public int? Nationality { get; set; }

    public int? JobPreference { get; set; }

    public string? ProfileBio { get; set; }

    public DateOnly? DoB { get; set; }

    public int? Role { get; set; }

    public string? PasswordHash { get; set; }

    /// <summary>Unique login handle chosen by the user (e.g. @john_doe). Used for authentication alongside email.</summary>
    public string? Username { get; set; }

    /// <summary>User's city/region (e.g. "Lisboa", "Porto"). Legacy text field – prefer LocationId.</summary>
    public string? Location { get; set; }

    /// <summary>FK to the Locations table. Used for structured location-based matching.</summary>
    public int? LocationId { get; set; }

    /// <summary>FK to the Countries table.</summary>
    public int? CountryId { get; set; }

    public virtual Location? LocationNav { get; set; }

    public virtual Country? CountryNav { get; set; }

    public string? ProfilePictureUrl { get; set; }

    public string? BannerUrl { get; set; }

    /// <summary>Subscription plan: 0=Free, 1=Pro, 2=Enterprise</summary>
    public int SubscriptionPlan { get; set; } = 0;

    /// <summary>URL of the document uploaded when requesting employer role.</summary>
    public string? EmployerRequestDocumentUrl { get; set; }

    /// <summary>Optional note/reason submitted with the employer role request.</summary>
    public string? EmployerRequestNote { get; set; }

    public bool IsOpenToWork { get; set; } = true;

    public virtual ICollection<AuditLog> AuditLogs{ get; set; } = new List<AuditLog>();

    public virtual ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();

    public virtual ICollection<ChatUser> ChatUsers { get; set; } = new List<ChatUser>();

    public virtual ICollection<Chat> Chats { get; set; } = new List<Chat>();

    public virtual ICollection<CompanyMember> CompanyMembers { get; set; } = new List<CompanyMember>();

    public virtual ICollection<EmployerCandidateHistory> EmployerCandidateHistories { get; set; } = new List<EmployerCandidateHistory>();

    public virtual ICollection<InterviewRound> InterviewRounds { get; set; } = new List<InterviewRound>();

    public virtual ICollection<JobApplication> JobApplications { get; set; } = new List<JobApplication>();

    public virtual ICollection<Notification> NotificationActorUsers { get; set; } = new List<Notification>();

    public virtual ICollection<Notification> NotificationUsers { get; set; } = new List<Notification>();

    public virtual ICollection<Opportunity> Opportunities { get; set; } = new List<Opportunity>();

    public virtual ICollection<PostComment> PostComments { get; set; } = new List<PostComment>();

    public virtual ICollection<PostReaction> PostReactions { get; set; } = new List<PostReaction>();

    public virtual ICollection<Post> Posts { get; set; } = new List<Post>();

    public virtual ICollection<ProfileEducation> ProfileEducations { get; set; } = new List<ProfileEducation>();

    public virtual ICollection<ProfileExperience> ProfileExperiences { get; set; } = new List<ProfileExperience>();

    public virtual ICollection<Session> Sessions { get; set; } = new List<Session>();

    public virtual ICollection<SkillEndorsement> SkillEndorsements { get; set; } = new List<SkillEndorsement>();

    public virtual ICollection<UserSkill> UserSkills { get; set; } = new List<UserSkill>();
}
