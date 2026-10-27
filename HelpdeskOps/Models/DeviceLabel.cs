namespace HelpdeskOps.Models;

// One sticker on the label sheet: the device and the QR code that opens /d/{id}
public record DeviceLabel(Device Device, string QrSvg);
