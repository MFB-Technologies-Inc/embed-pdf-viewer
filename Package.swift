// swift-tools-version: 6.2

import PackageDescription

let package = Package(
    name: "EmbedPDF",
    platforms: [.iOS(.v17), .macOS(.v14)],
    products: [
        .library(name: "EmbedPDF", targets: ["EmbedPDF"]),
        .library(name: "EmbedPDFUI", targets: ["EmbedPDFUI"]),
    ],
    targets: [
        .binaryTarget(
            name: "EmbedPDFBindings",
            url: "https://github.com/roanutil/embed-pdf-runtime/releases/download/apple-engine-8d0a810dc5a3b06f/EmbedPDFBindings.xcframework.zip",
            checksum: "dc2a8378686170f467b2ba0ea3f503030aaebd89ed511cc55c521d63ec07b509"
        ),
        .target(
            name: "EmbedPDF",
            dependencies: ["EmbedPDFBindings"],
            path: "platforms/apple/EmbedPDF/Sources/EmbedPDF",
            linkerSettings: [
                .linkedLibrary("c++"),
                .linkedFramework("CoreFoundation"),
                .linkedFramework("CoreGraphics"),
                .linkedFramework("Foundation"),
                .linkedFramework("AppKit", .when(platforms: [.macOS])),
            ]
        ),
        .target(
            name: "EmbedPDFUI",
            dependencies: ["EmbedPDF"],
            path: "platforms/apple/EmbedPDF/Sources/EmbedPDFUI"
        ),
    ]
)
