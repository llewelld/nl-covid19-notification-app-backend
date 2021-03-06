// Copyright 2020 De Staat der Nederlanden, Ministerie van Volksgezondheid, Welzijn en Sport.
// Licensed under the EUROPEAN UNION PUBLIC LICENCE v. 1.2
// SPDX-License-Identifier: EUPL-1.2

using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using NL.Rijksoverheid.ExposureNotification.BackEnd.Components.Icc.Models;

namespace NL.Rijksoverheid.ExposureNotification.BackEnd.Components.Icc.Auth.Code
{
    public interface IAuthCodeService
    {
        Task<string> GenerateAuthCodeAsync(ClaimsPrincipal claimsPrincipal);

        Task<List<AuthClaim>?> GetClaimsByAuthCodeAsync(string authCode);

        Task RevokeAuthCodeAsync(string authCode);
    }
}