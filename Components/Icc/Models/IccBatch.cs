// Copyright 2020 De Staat der Nederlanden, Ministerie van Volksgezondheid, Welzijn en Sport.
// Licensed under the EUROPEAN UNION PUBLIC LICENCE v. 1.2
// SPDX-License-Identifier: EUPL-1.2

using System;
using System.Collections.Generic;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.Icc;

namespace NL.Rijksoverheid.ExposureNotification.BackEnd.Components.ICC.Models
{
    public class IccBatch
    {
        public IccBatch(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException(nameof(id));

            Id = id;
            Batch = new List<InfectionConfirmationCodeEntity>();
        }

        public string Id { get; set; }
        public List<InfectionConfirmationCodeEntity> Batch { get; }
        public void AddIcc(InfectionConfirmationCodeEntity icc)
        {
            if (icc == null) 
                throw new ArgumentNullException(nameof(icc));

            Batch.Add(icc);
        }

    }
}