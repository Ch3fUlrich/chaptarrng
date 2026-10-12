---
category: changed
audience: operators
area: build
action: Configure SIXLABORS_LICENSE_KEY locally and as a GitHub Actions secret before building or publishing containers.
breaking: true
---
ImageSharp 4.1.3 builds require the complete issued Six Labors license contents through SIXLABORS_LICENSE_KEY for local builds, CI, and container publishing.
