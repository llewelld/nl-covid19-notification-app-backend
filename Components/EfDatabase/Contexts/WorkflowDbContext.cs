﻿// Copyright 2020 De Staat der Nederlanden, Ministerie van Volksgezondheid, Welzijn en Sport.
// Licensed under the EUROPEAN UNION PUBLIC LICENCE v. 1.2
// SPDX-License-Identifier: EUPL-1.2

using System;
using Microsoft.EntityFrameworkCore;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.Workflow;

namespace NL.Rijksoverheid.ExposureNotification.BackEnd.Components.EfDatabase.Contexts
{
    public class WorkflowDbContext : DbContext
    {
        public WorkflowDbContext(DbContextOptions options)
            : base(options)
        {
        }

        public DbSet<TekReleaseWorkflowStateEntity> KeyReleaseWorkflowStates { get; set; }
        public DbSet<TekEntity> TemporaryExposureKeys { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            if (modelBuilder == null) throw new ArgumentNullException(nameof(modelBuilder));

            modelBuilder.Entity<TekReleaseWorkflowStateEntity>().HasIndex(x => x.BucketId) /*.IsUnique()*/; //TODO genteks... 
            modelBuilder.Entity<TekReleaseWorkflowStateEntity>().HasIndex(x => x.ConfirmationKey)/*.IsUnique()*/; //TODO genteks... 
            modelBuilder.Entity<TekReleaseWorkflowStateEntity>().HasIndex(x => x.LabConfirmationId)/*.IsUnique()*/; //TODO genteks... 
            modelBuilder.Entity<TekReleaseWorkflowStateEntity>().HasIndex(x => x.PollToken)/*.IsUnique()*/; //TODO genteks... 

            modelBuilder.Entity<TekReleaseWorkflowStateEntity>().HasIndex(x => x.ValidUntil);
            //modelBuilder.Entity<TekReleaseWorkflowStateEntity>().HasIndex(x => x.Authorised);
            modelBuilder.Entity<TekReleaseWorkflowStateEntity>().HasIndex(x => x.AuthorisedByCaregiver);

            //TODO ensure cleanup jobs check the Published column of the Teks before deleting.
            modelBuilder
                .Entity<TekReleaseWorkflowStateEntity>()
                .HasMany(x => x.Keys)
                .WithOne(x => x.Owner)
                .IsRequired()
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TekEntity>().HasIndex(u => u.PublishingState);
            modelBuilder.Entity<TekEntity>().HasIndex(u => u.Region);
        }
    }
}