// swift-tools-version: 6.2

import PackageDescription

let package = Package(
    name: "EmbedPDF",
    platforms: [.iOS(.v17), .macOS(.v14)],
    products: [
        .library(name: "EmbedPDF", targets: ["EmbedPDF"]),
        .library(name: "EmbedPDFModels", targets: ["EmbedPDFModels"]),
        .library(name: "EmbedPDFUI", targets: ["EmbedPDFUI"]),
    ],
    targets: [
        .binaryTarget(
            name: "EmbedPDFBindings",
            url: "https://github.com/roanutil/embed-pdf-runtime/releases/download/apple-engine-8a472a28bcfb9c14/EmbedPDFBindings.xcframework.zip",
            checksum: "3660debb61e685bce3c3421fc7baa505088c467711bb474af157f04f3036207d"
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
            name: "EmbedPDFModels",
            dependencies: ["EmbedPDF"],
            path: "platforms/apple/EmbedPDF/Sources/EmbedPDFModels"
        ),
        .target(
            name: "EmbedPDFUI",
            dependencies: ["EmbedPDF", "EmbedPDFModels"],
            path: "platforms/apple/EmbedPDF/Sources/EmbedPDFUI"
        ),
    ]
)
