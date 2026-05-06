namespace KFS.Domain.Enums;

public enum BookingStatus
{
    Cart = 0,
    Confirmed = 1,
    Cancelled = 2,
    Expired = 3,
    RebookWindow = 4
}

public enum ZoneCode
{
    VIPAF = 0,
    VIPAM = 1,
    VIPBF = 2,
    VIPBM = 3,
    GUEST = 4,
    STAFF = 5,
    MEDIA = 6,
    VVIP = 7
}

public enum ZoneGroup
{
    None = 0,
    A = 1,
    B = 2
}

public enum ZoneSide
{
    None = 0,
    Female = 1,
    Male = 2
}

public enum ParentRole
{
    Mother = 0,
    Father = 1
}

public enum AdminPassType
{
    VVIP = 0,
    Guest = 1,
    Staff = 2,
    Media = 3
}

public enum ScannedItemType
{
    BookingItem = 0,
    AdminPass = 1
}

public enum ScanResult
{
    Valid = 0,
    AlreadyUsed = 1,
    Invalid = 2,
    Expired = 3
}

public enum ReminderType
{
    Unbooked = 0,
    DayBefore = 1
}

public enum ActorType
{
    Student = 0,
    Admin = 1,
    System = 2
}

public enum AdminRole
{
    SuperAdmin = 0,
    Admin = 1
}

public enum UserType
{
    Student = 0,
    Admin = 1
}

public enum PassOutputFormat
{
    Pdf = 0,
    Zip = 1
}
