namespace BrewAlert.Infrastructure.Hardware;

/// <summary>
/// Maps Linux <c>linux/input-event-codes.h</c> keycodes emitted by USB HID RFID readers
/// to ASCII characters or an Enter signal.
/// </summary>
public static class LinuxKeyCodeMapper
{
    public const ushort KeyLeftShift = 42;
    public const ushort KeyRightShift = 54;
    public const ushort KeyEnter = 28;
    public const ushort KeyKeypadEnter = 96;

    public static bool IsShift(ushort code) => code is KeyLeftShift or KeyRightShift;
    public static bool IsEnter(ushort code) => code is KeyEnter or KeyKeypadEnter;

    public static char? MapKey(ushort code, bool shift) => code switch
    {
        // Number row: KEY_1 (2) .. KEY_0 (11)
        2 => '1',
        3 => '2',
        4 => '3',
        5 => '4',
        6 => '5',
        7 => '6',
        8 => '7',
        9 => '8',
        10 => '9',
        11 => '0',
        12 => shift ? '_' : '-',

        // Keypad digits
        71 => '7',
        72 => '8',
        73 => '9',
        75 => '4',
        76 => '5',
        77 => '6',
        79 => '1',
        80 => '2',
        81 => '3',
        82 => '0',

        // QWERTY letter rows (stored uppercase for RFID hex/alphanumeric card IDs)
        16 => 'Q',
        17 => 'W',
        18 => 'E',
        19 => 'R',
        20 => 'T',
        21 => 'Y',
        22 => 'U',
        23 => 'I',
        24 => 'O',
        25 => 'P',
        30 => 'A',
        31 => 'S',
        32 => 'D',
        33 => 'F',
        34 => 'G',
        35 => 'H',
        36 => 'J',
        37 => 'K',
        38 => 'L',
        44 => 'Z',
        45 => 'X',
        46 => 'C',
        47 => 'V',
        48 => 'B',
        49 => 'N',
        50 => 'M',

        _ => null,
    };
}
