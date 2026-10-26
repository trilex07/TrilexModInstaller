// SPDX-License-Identifier: GPL-2.0-or-later
// Copyright (c) 2026 Trilex. Standalone local decoder; never linked into installer or mod.
#define main unused_upstream_main
#include "lzo_package.cpp"
#undef main
#include <algorithm>
#include <stdexcept>
#include <filesystem>

static constexpr size_t Limit=512u*1024u*1024u;
static void Require(bool ok) { if(!ok) throw std::runtime_error("Invalid or oversized UE3 compressed input"); }
static std::vector<uint8_t> Decode(const uint8_t* src,size_t len) {
    Require(len>=24 && len<=Limit && RdU32(src)==kPackageTag);
    const uint64_t block=RdU32(src+4), csz=RdU32(src+8), usz=RdU32(src+12);
    Require(block>0 && block<=1024*1024 && usz>0 && usz<=Limit);
    const size_t count=size_t((usz+block-1)/block), header=16+count*8;
    Require(header<len && csz<=len-header && len-header-csz<65536);
    Require(std::all_of(src+header+size_t(csz),src+len,[](uint8_t b){return b==0;}));
    len=header+size_t(csz);
    std::vector<uint8_t> result(size_t(usz),0);size_t input=header,output=0;
    for(size_t i=0;i<count;++i) {
        const size_t c=RdU32(src+16+i*8), u=RdU32(src+20+i*8);
        Require(c>0 && u>0 && u<=block && c<=len-input && u<=result.size()-output);
        if(c==u)memcpy(result.data()+output,src+input,u);
        else {size_t used=0;const char* error=nullptr;Require(Lzo1xDecompress(src+input,c,result.data()+output,u,&used,&error)&&used==u);}
        input+=c;output+=u;
    }
    Require(input==len && output==result.size());return result;
}
static std::vector<uint8_t> Logical(const std::vector<uint8_t>& src) {
    Require(src.size()>=128 && RdU32(src.data())==kPackageTag);
    size_t table=0,count=0,matches=0;
    for(size_t p=12;p+24<=std::min<size_t>(src.size(),256);++p) {
        if(RdU32(src.data()+p)!=2)continue;
        const size_t n=RdU32(src.data()+p+4);if(!n||n>32||p+8+n*16>src.size())continue;
        bool valid=true;
        for(size_t j=0;j<n;++j){const auto* r=src.data()+p+8+j*16;const uint64_t off=RdU32(r+8),len=RdU32(r+12);if(len<24||off+len>src.size()||RdU32(src.data()+size_t(off))!=kPackageTag){valid=false;break;}}
        if(valid){table=p;count=n;++matches;}
    }
    Require(matches==1);const size_t first=RdU32(src.data()+table+8),physical=first+count*16;
    Require(first>=table+8 && first<=Limit && physical<=src.size() && table+8+count*16<=physical);
    std::vector<uint8_t> out(src.begin(),src.begin()+table+8);out.insert(out.end(),src.begin()+table+8+count*16,src.begin()+physical);Require(out.size()==first);
    for(size_t j=0;j<count;++j){const auto* r=src.data()+table+8+j*16;const size_t off=RdU32(r),unc=RdU32(r+4),pos=RdU32(r+8),len=RdU32(r+12);Require(off==out.size()&&unc<=Limit-out.size());auto part=Decode(src.data()+pos,len);Require(part.size()==unc);out.insert(out.end(),part.begin(),part.end());}
    memset(out.data()+table,0,8);return out;
}
int wmain(int argc,wchar_t** argv) {
    try {
        // Explicit source, fresh output and codec only. No default in-place operation.
        Require(argc==4 && (std::wstring(argv[1])==L"ue3-whole-lzo"||std::wstring(argv[1])==L"ue3-logical-lzo"));
        const auto size=std::filesystem::file_size(argv[2]);Require(size>=16&&size<=Limit);
        FILE* f=nullptr;Require(_wfopen_s(&f,argv[2],L"rb")==0&&f);std::vector<uint8_t> src(size_t(size),0);const size_t got=fread(src.data(),1,src.size(),f);fclose(f);Require(got==src.size());
        auto out=std::wstring(argv[1])==L"ue3-whole-lzo"?Decode(src.data(),src.size()):Logical(src);
        Require(out.size()>=8 && RdU32(out.data())==kPackageTag);
        Require(_wfopen_s(&f,argv[3],L"wbx")==0&&f);const size_t written=fwrite(out.data(),1,out.size(),f);const int closed=fclose(f);Require(written==out.size()&&closed==0);
        puts("PASS bounded local UE3 decode");return 0;
    } catch(...) {fputs("REFUSED local UE3 decode\n",stderr);return 1;}
}

