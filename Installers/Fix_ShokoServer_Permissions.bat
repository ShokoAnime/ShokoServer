netsh http add urlacl url=http://+:8111/JMMServerPlex sddl=D:(A;;GA;;;S-1-1-0)
netsh http add urlacl url=http://+:8111/JMMServerStreaming sddl=D:(A;;GA;;;S-1-1-0)
netsh http add urlacl url=http://+:8111/JMMServerImage sddl=D:(A;;GA;;;S-1-1-0)
netsh http add urlacl url=http://+:8111/ sddl=D:(A;;GA;;;S-1-1-0)
icacls C:\ProgramData\ShokoServer /inheritance:r
icacls C:\ProgramData\ShokoServer /grant *S-1-1-0:(OI)(CI)F /T /inheritance:e