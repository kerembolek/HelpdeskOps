using HelpdeskOps.Services.Kvkk;

namespace HelpdeskOps.Models;

// The privacy notice page; ReadAt is set when the signed-in user has confirmed reading the current version
public record PrivacyViewModel(KvkkOptions Kvkk, DateTime? ReadAt);
