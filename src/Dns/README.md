# Credits

This DNS code was originally written by Alphons van der Heijden,
from his [DNS.NET Resolver project](https://www.codeproject.com/Articles/23673/DNS-NET-Resolver-C),
which was in turn modified for Claire Novotny's [Zeroconf](https://github.com/novotnyllc/Zeroconf)
Bonjour/MDNS discovery project.

# Modifications

I, [Don Cross](https://github.com/cosinekitty/), have added some things useful for my own needs:

- The `RecordWriter` class needed for serializing back to wire format for publishing.

- Implemented support for [`NSEC` records](https://datatracker.ietf.org/doc/html/rfc4034#section-4.1).
