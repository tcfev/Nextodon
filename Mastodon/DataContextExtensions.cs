﻿namespace Mastodon;

public static class DataContextExtensions
{
    public static async Task<Grpc.Status> GetStatusById(this DataContext db, ServerCallContext context, string id, string? meId, bool throwIfNotFound = true)
    {
        var status = await db.Status.FindByIdAsync(id);
        if (status == null)
        {
            if (throwIfNotFound)
            {
                throw new RpcException(new global::Grpc.Core.Status(StatusCode.NotFound, string.Empty));
            }
            else
            {
                return new Grpc.Status { Id = id };
            }
        }

        var owner = await db.Account.FindByIdAsync(status.AccountId);

        var result = status.ToGrpc(owner!);
        var account = owner!.ToGrpc();
        result.Account = account;

        result.Uri = context.GetUrlPath($"statuses/{status.Id}");
        result.Url = context.GetUrlPath($"statuses/{status.Id}");

        if (!string.IsNullOrWhiteSpace(status.ReblogedFromId))
        {
            result.Reblog = await db.GetStatusById(context, status.ReblogedFromId, meId, false);
        }

        {
            var filter1 = Builders<Data.Status>.Filter.Ne(x => x.Deleted, true);
            var filter2 = Builders<Data.Status>.Filter.Eq(x => x.InReplyToId, status.Id);
            result.RepliesCount = (uint)(await db.Status.CountDocumentsAsync(filter1 & filter2));
        }

        var mediaIds = status.MediaIds;

        if (mediaIds != null)
        {
            foreach (var mediaId in mediaIds)
            {
                var media = await db.Media.FindByIdAsync(mediaId);

                result.MediaAttachments.Add(media!.ToGrpc());
            }
        }
        result.Poll = null;

        if (status.Poll != null)
        {
            result.Poll = new Grpc.Poll
            {
                Id = status.Id,
                Kind = Grpc.PollKind.Priority,
                Expired = false,
                ExpiresAt = Timestamp.FromDateTime(DateTime.UtcNow.AddDays(1)),
                VotersCount = 10000,
                VotesCount = 10000,
                Voted = true,
            };

            foreach (var option in status.Poll.Options)
            {
                result.Poll.Options.Add(new Grpc.Poll.Types.Option { Title = option, VotesCount = 100, });
            }
        }

        {
            var filter1 = Builders<Data.Status>.Filter.Ne(x => x.Deleted, true);
            var filter2 = Builders<Data.Status>.Filter.Eq(x => x.ReblogedFromId, status.Id);
            result.ReblogsCount = (uint)(await db.Status.CountDocumentsAsync(filter1 & filter2));
        }

        if (!string.IsNullOrWhiteSpace(meId))
        {
            var filter1 = Builders<Data.Status>.Filter.Ne(x => x.Deleted, true);
            var filter2 = Builders<Data.Status>.Filter.Eq(x => x.ReblogedFromId, status.Id);
            var filter3 = Builders<Data.Status>.Filter.Eq(x => x.AccountId, meId);
            result.Reblogged = (await db.Status.CountDocumentsAsync(filter1 & filter2 & filter3)) > 0;
        }


        if (!string.IsNullOrWhiteSpace(meId))
        {
            var existFilter = Builders<Data.Status_Account>.Filter.Ne(x => x.Deleted, true);
            var sidFilter = Builders<Data.Status_Account>.Filter.Eq(x => x.StatusId, status.Id) & existFilter;
            var meFilter = Builders<Data.Status_Account>.Filter.Eq(x => x.AccountId, meId);
            var favFilter = Builders<Data.Status_Account>.Filter.Eq(x => x.Favorite, true);

            var favCount = db.StatusAccount.CountDocumentsAsync(sidFilter & favFilter);

            result.FavouritesCount = (uint)(await favCount);
        }

        if (!string.IsNullOrWhiteSpace(meId))
        {
            var statusAccount = await db.StatusAccount.UpdateAsync(status.Id, meId);

            result.Muted = statusAccount.Mute;
            result.Pinned = statusAccount.Pin;
            result.Bookmarked = statusAccount.Bookmark;
            result.Favourited = statusAccount.Favorite;
        }

        return result;
    }

    public static async Task<Status_Account> UpdateAsync(this IMongoCollection<Status_Account> collection, string statusId, string accountId, bool? favorite = null, bool? bookmark = null, bool? pin = null, bool? mute = null, CancellationToken cancellationToken = default)
    {
        var filter1 = Builders<Data.Status_Account>.Filter.Eq(x => x.StatusId, statusId);
        var filter2 = Builders<Data.Status_Account>.Filter.Eq(x => x.AccountId, accountId);

        var update = Builders<Data.Status_Account>.Update
            .SetOnInsert(x => x.StatusId, statusId)
            .SetOnInsert(x => x.AccountId, accountId)
            .SetOnInsert(x => x.Deleted, false)
            .SetOnInsert(x => x.Favorite, favorite ?? false)
            .SetOnInsert(x => x.Bookmark, bookmark ?? false)
            .SetOnInsert(x => x.Pin, pin ?? false)
            .SetOnInsert(x => x.Mute, mute ?? false);



        var options = new FindOneAndUpdateOptions<Status_Account, Status_Account>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After,
        };

        var sa = await collection.FindOneAndUpdateAsync(filter1 & filter2, update, options, cancellationToken);


        UpdateDefinition<Status_Account>? u = null;

        if (favorite != null)
        {
            var f = Builders<Data.Status_Account>.Update.Set(x => x.Favorite, favorite!);
            if (u == null)
            {
                u = f;
            }
            else
            {
                u = Builders<Data.Status_Account>.Update.Combine(u, f);
            }
        }

        if (bookmark != null)
        {
            var f = Builders<Data.Status_Account>.Update.Set(x => x.Bookmark, bookmark!);

            if (u == null)
            {
                u = f;
            }
            else
            {
                u = Builders<Data.Status_Account>.Update.Combine(u, f);
            }
        }

        if (pin != null)
        {
            var f = Builders<Data.Status_Account>.Update.Set(x => x.Pin, pin!);
            if (u == null)
            {
                u = f;
            }
            else
            {
                u = Builders<Data.Status_Account>.Update.Combine(u, f);
            }
        }

        if (mute != null)
        {
            var f = Builders<Data.Status_Account>.Update.Set(x => x.Mute, mute!);
            if (u == null)
            {
                u = f;
            }
            else
            {
                u = Builders<Data.Status_Account>.Update.Combine(u, f);
            }
        }


        if (u == null)
        {
            return sa;
        }

        var filter = Builders<Data.Status_Account>.Filter.Eq(x => x.Id, sa.Id);
        return await collection.FindOneAndUpdateAsync(filter, u, options, cancellationToken);
    }
}
