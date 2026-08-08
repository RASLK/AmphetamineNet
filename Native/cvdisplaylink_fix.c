/*
 * macOS 26+: CVDisplayLinkCreateWithActiveCGDisplays returns kCVReturnInvalidArgument (-6661).
 * Avalonia.Native still uses that API for RenderTimer. Interpose to CreateWithCGDisplay(main).
 */
#include <CoreGraphics/CoreGraphics.h>
#include <CoreVideo/CoreVideo.h>

CVReturn AmphetamineNet_CVDisplayLinkCreateWithActiveCGDisplays(
    CVDisplayLinkRef _Nullable * _Nonnull displayLinkOut)
{
    return CVDisplayLinkCreateWithCGDisplay(CGMainDisplayID(), displayLinkOut);
}

#define DYLD_INTERPOSE(_replacement, _replacee) \
    __attribute__((used)) static struct { \
        const void* replacement; \
        const void* replacee; \
    } _interpose_##_replacee __attribute__((section("__DATA,__interpose"))) = { \
        (const void*)(unsigned long)&_replacement, \
        (const void*)(unsigned long)&_replacee \
    };

DYLD_INTERPOSE(AmphetamineNet_CVDisplayLinkCreateWithActiveCGDisplays,
               CVDisplayLinkCreateWithActiveCGDisplays)
