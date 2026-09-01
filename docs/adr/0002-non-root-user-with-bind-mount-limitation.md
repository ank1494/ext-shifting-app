# Run as a non-root user, accepting a Linux bind-mount limitation

The container runs as a non-root user rather than as `root`. The application
writes to two directories at runtime: the git submodule at `/m2/ext-shifting`,
which the M2 Process Manager uses as its working directory, and the Docker
volume at `/output`. The first is baked into the image, so a build-time `chown`
fixes its ownership permanently. The second is a bind mount, so the host
supplies the owner and the Dockerfile cannot influence it.

## Considered options

Switching `/output` to a named Docker volume would let Docker set ownership
correctly and remove the limitation entirely. It was rejected: the users are
mathematicians who need to open their results in an ordinary folder on their own
machine, and a named volume hides the output inside Docker's storage. Preserving
that is worth more than a clean permissions story.

## Consequences

On Docker Desktop for macOS and Windows this works, because bind-mount ownership
is virtualised. On a Linux host, the non-root user may be unable to write to
`./output` if the host directory is not writable by the container's UID. This is
a documented limitation in the README, not a bug to be fixed by reverting to
`root`.
