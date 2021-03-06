﻿// Copyright 2020 De Staat der Nederlanden, Ministerie van Volksgezondheid, Welzijn en Sport.
// Licensed under the EUROPEAN UNION PUBLIC LICENCE v. 1.2
// SPDX-License-Identifier: EUPL-1.2

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.EfDatabase;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.EfDatabase.Contexts;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.EfDatabase.Entities;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.Services.Signing.Signers;
using Serilog;

namespace NL.Rijksoverheid.ExposureNotification.BackEnd.Components.Content
{
    public class NlContentResignCommand
    {
        private readonly Func<ContentDbContext> _ContentDbContext;
        private readonly IContentSigner _ContentSigner;
        private readonly ILogger<NlContentResignCommand> _Logger;

        private string _ContentEntryName;
        private string _ToType;

        /// <summary>
        /// Comparer ensures content is equivalent so that items are not re-signed more than once
        /// </summary>
        private class ContentEntityComparer : IEqualityComparer<ContentEntity>
        {
            public bool Equals(ContentEntity left, ContentEntity right)
             => left.Created == right.Created
               && left.Release == right.Release
               && left.PublishingId == right.PublishingId;

            public int GetHashCode(ContentEntity obj) => HashCode.Combine(obj.Created, obj.Release, obj.PublishingId);
        }

        public NlContentResignCommand(Func<ContentDbContext> contentDbContext, IContentSigner contentSigner, ILogger<NlContentResignCommand> logger)
        {
            _ContentDbContext = contentDbContext ?? throw new ArgumentNullException(nameof(contentDbContext));
            _ContentSigner = contentSigner ?? throw new ArgumentNullException(nameof(contentSigner));
            _Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Copy and sign all content of 'fromType' that has not already been re-signed.
        /// </summary>
        public async Task Execute(string fromType, string toType, string contentEntryName)
        {
            _ToType = toType;
            _ContentEntryName = contentEntryName;

            var db = _ContentDbContext();

            var fromItems = db.Content.Where(x => x.Type == fromType).ToArray();
            var toItems = db.Content.Where(x => x.Type == toType).ToArray();
            var todo = fromItems.Except(toItems,  new ContentEntityComparer()).ToArray();

            var sb = new StringBuilder();
            sb.AppendLine($"Re-signing {todo.Length} items:");
            foreach (var i in todo)
                sb.AppendLine($"PK:{i.Id} PublishingId:{i.PublishingId} Created:{i.Created:O} Release:{i.Release:O}");

            var m = sb.ToString();
            _Logger.LogInformation(m);

            foreach (var i in todo)
                await Resign(i);

            _Logger.LogInformation("Re-signing complete,");
        }

        private async Task Resign(ContentEntity item)
        {
            await using var db = _ContentDbContext();
            await using var tx = db.BeginTransaction();

            var content = await ReplaceSig(item.Content);
            var e = new ContentEntity
            {
                Created = item.Created,
                Release = item.Release,
                ContentTypeName = item.ContentTypeName,
                Content = content,
                Type = _ToType,
                PublishingId = item.PublishingId
            };
            await db.Content.AddAsync(e);
            db.SaveAndCommit();
        }

        private async Task<byte[]> ReplaceSig(byte[] archiveBytes)
        {
            await using var m = new MemoryStream();
            m.Write(archiveBytes, 0, archiveBytes.Length);
            using (var archive = new ZipArchive(m, ZipArchiveMode.Update, true))
            {
                var content = archive.ReadEntry(_ContentEntryName);
                var sig = _ContentSigner.GetSignature(content);
                await archive.ReplaceEntry(ZippedContentEntryNames.NLSignature, sig);
            }
            return m.ToArray();
        }
    }
}