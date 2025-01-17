﻿namespace Nextodon;

public static class PostgresExtensions
{
    public static async Task<bool> IsStatusFavouritedByAsync(this Data.PostgreSQL.MastodonContext db, long statusId, long accountId)
    {
        var any = await (from x in db.Favourites
                         where x.StatusId == statusId && x.AccountId == accountId
                         select x).AnyAsync();

        return any;
    }

    public static async Task<bool> IsStatusBookmarkedByAsync(this Data.PostgreSQL.MastodonContext db, long statusId, long accountId)
    {
        var any = await (from x in db.Bookmarks
                         where x.StatusId == statusId && x.AccountId == accountId
                         select x).AnyAsync();

        return any;
    }

    public static async Task<Data.PostgreSQL.Models.Notification> NotifyAsync(this Data.PostgreSQL.MastodonContext db, long accountId, long fromAccountId, long activityId, string activityType, string? type, DateTime? now = null, CancellationToken cancellationToken = default)
    {
        now ??= DateTime.UtcNow;

        var notification = new Data.PostgreSQL.Models.Notification
        {
            FromAccountId = fromAccountId,
            AccountId = accountId,
            CreatedAt = now.Value,
            UpdatedAt = now.Value,
            ActivityId = activityId,
            Type = type,
            ActivityType = activityType,
        };

        db.Notifications.Add(notification);
        await db.SaveChangesAsync(cancellationToken);

        return notification;
    }

    public static async Task<int> GetStatusFavouritesCountAsync(this Data.PostgreSQL.MastodonContext db, long statusId, CancellationToken cancellationToken = default)
    {
        var count = await (from x in db.Favourites
                           where x.StatusId == statusId
                           select x).CountAsync(cancellationToken: cancellationToken);

        return count;
    }

    public static async Task<int> GetStatusBookmarksCountAsync(this Data.PostgreSQL.MastodonContext db, long statusId, CancellationToken cancellationToken = default)
    {
        var count = await (from x in db.Bookmarks
                           where x.StatusId == statusId
                           select x).CountAsync(cancellationToken: cancellationToken);

        return count;
    }

    public static async Task BookmarkStatusAsync(this Data.PostgreSQL.MastodonContext db, long statusId, long accountId, DateTime? now = null, CancellationToken cancellationToken = default)
    {
        now ??= DateTime.UtcNow;

        var query = from x in db.Bookmarks
                    where x.StatusId == statusId && x.AccountId == accountId
                    select x;

        var bookmark = await query.FirstOrDefaultAsync(cancellationToken: cancellationToken);

        if (bookmark == null)
        {
            bookmark = new Data.PostgreSQL.Models.Bookmark
            {
                AccountId = accountId,
                StatusId = statusId,
                CreatedAt = now.Value,
                UpdatedAt = now.Value,
            };
        }

        bookmark.UpdatedAt = now.Value;

        db.Bookmarks.Update(bookmark);
        await db.SaveChangesAsync(cancellationToken);
    }

    public static async Task UnbookmarkStatusAsync(this Data.PostgreSQL.MastodonContext db, long statusId, long accountId, CancellationToken cancellationToken = default)
    {
        await db.Bookmarks.Where(x => x.StatusId == statusId && x.AccountId == accountId).ExecuteDeleteAsync(cancellationToken);
    }

    public static async Task FavouriteStatusAsync(this Data.PostgreSQL.MastodonContext db, long statusId, long accountId, DateTime? now = null, CancellationToken cancellationToken = default)
    {
        now ??= DateTime.UtcNow;

        var query = from x in db.Favourites
                    where x.StatusId == statusId && x.AccountId == accountId
                    select x;

        var favourite = await query.FirstOrDefaultAsync(cancellationToken: cancellationToken);

        if (favourite == null)
        {
            favourite = new Data.PostgreSQL.Models.Favourite
            {
                AccountId = accountId,
                StatusId = statusId,
                CreatedAt = now.Value,
                UpdatedAt = now.Value,
            };
        }

        favourite.UpdatedAt = now.Value;

        db.Favourites.Update(favourite);
        await db.SaveChangesAsync(cancellationToken);
    }


    public static async Task UnfavouriteStatusAsync(this Data.PostgreSQL.MastodonContext db, long statusId, long accountId, CancellationToken cancellationToken = default)
    {
        await db.Favourites.Where(x => x.StatusId == statusId && x.AccountId == accountId).ExecuteDeleteAsync(cancellationToken);
    }

    public static async Task<Grpc.Status> ToGrpc(this Data.PostgreSQL.Models.Status i, Data.PostgreSQL.Models.Account? me, Data.PostgreSQL.MastodonContext db, ServerCallContext context)
    {
        var v = new Grpc.Status
        {
            Id = i.Id.ToString(),
            Text = i.Text,
            Content = i.Text,
            SpoilerText = i.SpoilerText,
            Sensitive = i.Sensitive,
            CreatedAt = i.CreatedAt.ToGrpc(),
            EditedAt = i.EditedAt?.ToGrpc(),
            Visibility = Grpc.Visibility.Public,
        };

        if (i.Language != null)
        {
            v.Language = i.Language;
        }

        if (i.InReplyToId != null)
        {
            v.InReplyToId = i.InReplyToId?.ToString();
        }

        if (i.InReplyToAccountId != null)
        {
            v.InReplyToAccountId = i.InReplyToAccountId?.ToString();
        }

        if (i.Uri != null)
        {
            v.Uri = i.Uri;
        }

        if (i.Url != null)
        {
            v.Url = i.Url;
        }

        bool isFavourited = false;
        bool isBookmarked = false;

        if (me != null)
        {
            isFavourited = await db.IsStatusFavouritedByAsync(i.Id, me.Id);
            isBookmarked = await db.IsStatusBookmarkedByAsync(i.Id, me.Id);
        }

        v.Favourited = isFavourited;
        v.Bookmarked = isBookmarked;

        v.FavouritesCount = (uint)await db.GetStatusFavouritesCountAsync(i.Id);

        var account = await db.Accounts.FindAsync(i.AccountId);
        if (account != null)
        {
            v.Account = await account.ToGrpc(me, db, context);
        }

        var pollQuery = from x in db.Polls
                        where x.StatusId == i.Id
                        select x;

        var poll = await pollQuery.FirstOrDefaultAsync();
        if (poll != null)
        {
            v.Poll = await poll.ToGrpc(db, context);
        }

        var mediaAttachmentsQuery = from x in db.MediaAttachments
                                    where x.StatusId == i.Id
                                    select x;

        var medias = await mediaAttachmentsQuery.ToListAsync();
        foreach (var media in medias)
        {
            var mediaAttachment = await media.ToGrpc(db, context);
            v.MediaAttachments.Add(mediaAttachment);
        }

        return v;
    }

    public static Task<Grpc.Account> ToGrpc(this Data.PostgreSQL.Models.Account i, Data.PostgreSQL.Models.Account? me, Data.PostgreSQL.MastodonContext db, ServerCallContext context)
    {
        var v = new Grpc.Account
        {
            CreatedAt = i.CreatedAt.ToGrpc(),
            DisplayName = i.DisplayName,
            Id = i.Id.ToString(),
            Note = i.Note,
            Username = i.Username,
        };

        if (i.Url != null)
        {
            v.Url = i.Url;
        }

        return Task.FromResult(v);
    }

    public static async Task<Grpc.Poll> ToGrpc(this Data.PostgreSQL.Models.Poll i, Data.PostgreSQL.MastodonContext db, ServerCallContext context)
    {
        var v = new Grpc.Poll
        {
            Id = i.Id.ToString(),
            ExpiresAt = i.ExpiresAt?.ToGrpc(),
            Multiple = i.Multiple,
        };

        var votes = await (from x in db.PollVotes
                           where x.PollId == i.Id
                           select x).ToListAsync();



        for (int x = 0; x < i.Options.Length; x++)
        {
            var op = i.Options[x];
            var count = votes.Where(z => z.Choice == x).Count();

            v.Options.Add(new Grpc.Poll.Types.Option { Title = op, VotesCount = (uint)count });
        }

        return v;
    }

    public static Task<Grpc.MediaAttachment> ToGrpc(this Data.PostgreSQL.Models.MediaAttachment i, Data.PostgreSQL.MastodonContext db, ServerCallContext context)
    {
        var v = new Grpc.MediaAttachment
        {
            Id = i.Id.ToString(),
            Type = i.Type.ToString(),
            RemoteUrl = i.RemoteUrl,
        };

        if (i.Description != null)
        {
            v.Description = i.Description;
        }

        if (i.Blurhash != null)
        {
            v.Blurhash = i.Blurhash;
        }

        return Task.FromResult(v);
    }
}
