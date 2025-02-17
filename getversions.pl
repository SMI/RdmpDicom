#!/usr/bin/perl -w

use strict;

open my $rdmp, '<', "Rdmp/SharedAssemblyInfo.cs" or die "Rdmp/SharedAssemblyInfo.cs:$1\n";
while(<$rdmp>) {
	print "rdmpversion=$1\n" if /AssemblyInformationalVersion\("([^\"]+)"\)/;
}
open my $assembly, '<', "SharedAssemblyInfo.cs" or die "SharedAssemblyInfo.cs:$1\n";
while(<$assembly>) {
    print "version=$1\n" if /AssemblyInformationalVersion\("([^\"]+)"\)/;
}
