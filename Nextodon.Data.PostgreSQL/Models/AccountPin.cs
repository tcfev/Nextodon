﻿using System;
using System.Collections.Generic;

namespace Nextodon.Data.PostgreSQL.Models;

public partial class AccountPin
{
    public long Id { get; set; }

    public long? AccountId { get; set; }

    public long? TargetAccountId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Account? Account { get; set; }

    public virtual Account? TargetAccount { get; set; }
}
