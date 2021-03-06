﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Common.Log;
using MAVN.Common.Encryption;
using Lykke.Common.Log;
using MAVN.Persistence.PostgreSQL.Legacy;
using MAVN.Service.CustomerProfile.Domain.Enums;
using MAVN.Service.CustomerProfile.Domain.Models;
using MAVN.Service.CustomerProfile.Domain.Repositories;
using MAVN.Service.CustomerProfile.MsSqlRepositories.Entities;
using Microsoft.EntityFrameworkCore;

namespace MAVN.Service.CustomerProfile.MsSqlRepositories.Repositories
{
    public class PartnerContactRepository : IPartnerContactRepository
    {
        private readonly PostgreSQLContextFactory<CustomerProfileContext> _contextFactory;
        private readonly IEncryptionService _encryptionService;
        private readonly ILog _log;

        public PartnerContactRepository(
            PostgreSQLContextFactory<CustomerProfileContext> contextFactory,
            IEncryptionService encryptionService,
            ILogFactory logFactory)
        {
            _contextFactory = contextFactory;
            _encryptionService = encryptionService;
            _log = logFactory.CreateLog(this);
        }

        public async Task<IPartnerContact> GetByLocationIdAsync(string locationId)
        {
            using (var context = _contextFactory.CreateDataContext())
            {
                var result = await context.PartnerContacts
                    .FirstOrDefaultAsync(c => c.LocationId == locationId);

                if (result == null)
                    return null;

                result = _encryptionService.Decrypt(result);

                return new PartnerContactModel
                {
                    LocationId = result.LocationId,
                    FirstName = result.FirstName,
                    LastName = result.LastName,
                    Email = result.Email,
                    PhoneNumber = result.PhoneNumber
                };
            }
        }

        public async Task<IPartnerContact> GetByEmailAsync(string email)
        {
            var encryptedEmail = _encryptionService.EncryptValue(email);

            using (var context = _contextFactory.CreateDataContext())
            {
                var result = await context.PartnerContacts
                    .FirstOrDefaultAsync(c => c.Email == encryptedEmail);

                if (result == null)
                    return null;

                result = _encryptionService.Decrypt(result);

                return new PartnerContactModel
                {
                    LocationId = result.LocationId,
                    FirstName = result.FirstName,
                    LastName = result.LastName,
                    Email = result.Email,
                    PhoneNumber = result.PhoneNumber
                };
            }
        }

        public async Task<IPartnerContact> GetByPhoneAsync(string phone)
        {
            var encryptedPhone = _encryptionService.EncryptValue(phone);

            using (var context = _contextFactory.CreateDataContext())
            {
                var result = await context.PartnerContacts
                    .FirstOrDefaultAsync(c => c.PhoneNumber == encryptedPhone);

                if (result == null)
                    return null;

                result = _encryptionService.Decrypt(result);

                return new PartnerContactModel
                {
                    LocationId = result.LocationId,
                    FirstName = result.FirstName,
                    LastName = result.LastName,
                    Email = result.Email,
                    PhoneNumber = result.PhoneNumber
                };
            }
        }

        public async Task DeleteIfExistsAsync(string locationId)
        {
            using (var context = _contextFactory.CreateDataContext())
            {
                using (var transaction = context.Database.BeginTransaction())
                {
                    try
                    {
                        var entity = await context.PartnerContacts
                            .FirstOrDefaultAsync(c => c.LocationId == locationId);

                        if (entity == null)
                            return;

                        var archiveEntity = PartnerContactArchiveEntity.Create(entity);

                        context.PartnerContactsArchive.Add(archiveEntity);

                        context.PartnerContacts.Remove(entity);

                        await context.SaveChangesAsync();

                        transaction.Commit();
                    }
                    catch (Exception e)
                    {
                        _log.Error(e, "Error occured while deleting partner contact ", $"locationId = {locationId}");
                    }
                }
            }
        }

        public async Task<IEnumerable<IPartnerContact>> GetPaginatedAsync(int skip, int take)
        {
            using (var context = _contextFactory.CreateDataContext())
            {
                var partners = await context.PartnerContacts
                    .Skip(skip)
                    .Take(take)
                    .Select(c => _encryptionService.Decrypt(c))
                    .Select(_selectExpression)
                    .ToArrayAsync();

                return partners;
            }
        }

        public async Task<int> GetTotalAsync()
        {
            using (var context = _contextFactory.CreateDataContext())
            {
                return await context.PartnerContacts.CountAsync();
            }
        }

        public async Task CreateOrUpdateAsync(PartnerContactModel partnerContact)
        {
            using (var context = _contextFactory.CreateDataContext())
            {
                var existentPartnerContact = await context.PartnerContacts
                    .FirstOrDefaultAsync(c => c.LocationId == partnerContact.LocationId);

                if (existentPartnerContact != null)
                {
                    existentPartnerContact = _encryptionService.Decrypt(existentPartnerContact);

                    existentPartnerContact.FirstName = partnerContact.FirstName;
                    existentPartnerContact.LastName = partnerContact.LastName;
                    existentPartnerContact.PhoneNumber = partnerContact.PhoneNumber;
                    existentPartnerContact.Email = partnerContact.Email;

                    existentPartnerContact = _encryptionService.Encrypt(existentPartnerContact);

                    context.PartnerContacts.Update(existentPartnerContact);
                }
                else
                {
                    var entity = PartnerContactEntity.Create(partnerContact);

                    entity = _encryptionService.Encrypt(entity);

                    context.PartnerContacts.Add(entity);
                }

                await context.SaveChangesAsync();
            }
        }

        private readonly Expression<Func<PartnerContactEntity, PartnerContactModel>> _selectExpression =
            entity => new PartnerContactModel
            {
                LocationId = entity.LocationId,
                Email = entity.Email,
                FirstName = entity.FirstName,
                LastName = entity.LastName,
                PhoneNumber = entity.PhoneNumber
            };
    }
}
