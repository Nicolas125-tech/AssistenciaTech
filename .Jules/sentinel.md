## 2026-08-14 - Non-root container execution and docker-compose security hardening

**Vulnerability:** The application was configured to run as root within the Docker container (missing USER entrypoint). Additionally, the Docker Compose services lacked the `no-new-privileges:true` option and did not run with a read-only root filesystem, presenting potential privilege escalation and container persistence vectors.

**Learning:** Leaving the root user active inside container builds allows containerized processes to execute commands with elevated privileges, potentially compromising the host in case of container escapes. Similarly, allowing writable filesystems by default in Docker Compose allows compromised containers to write payloads and persist after restart.

**Prevention:** Always define a dedicated non-root user (like the built-in `app` user in .NET 8) in Dockerfiles and set `USER <username>` before the entrypoint, ensuring correct directory permissions. In `docker-compose.yml`, apply `no-new-privileges:true` and `read_only: true` where possible, using `tmpfs` mounts for required writable directories.
