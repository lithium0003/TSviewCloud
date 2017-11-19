// これは メイン DLL ファイルです。

#include "stdafx.h"

#include "Secretbox.h"

namespace Secretbox {

	public ref class Poly1305 {
	public:
		static const int TagSize = 16;

		static bool Verify(array<Byte>^ mac, array<Byte>^ msg, array<Byte>^ key)
		{
			return Verify(mac, msg, key, 0);
		}

		static bool Verify(array<Byte>^ mac, array<Byte>^ msg, array<Byte>^ key, int offset)
		{
			if (key->Length != 32) return false;
			if (mac->Length != TagSize) return false;

			array<Byte>^ t;
			Sum(t, msg, key, offset);
			pin_ptr<Byte> pt = &t[0];
			pin_ptr<Byte> pmac = &mac[0];
			return memcmp(pt, pmac, TagSize) == 0;
		}

		static void Sum([Runtime::InteropServices::Out] array<Byte>^% out, array<Byte>^ m, array<Byte>^ key, int offset)
		{
			out = gcnew array<Byte>(TagSize);
			pin_ptr<Byte> pkey = &key[0];
			pin_ptr<Byte> pout = &out[0];

			if (m->Length > offset) {
				pin_ptr<Byte> mPtr = &m[offset];

				poly1305(pout, mPtr, m->Length - offset, pkey);
			}
		}

	private:
		// https://github.com/golang/crypto/blob/master/poly1305/sum_ref.go
		static void poly1305(Byte* out, Byte* msg, UInt64 mlen, Byte* key)
		{
			UInt32 h0, h1, h2, h3, h4; // the hash accumulators
			UInt64 r0, r1, r2, r3, r4; // the r part of the key

			h0 = h1 = h2 = h3 = h4 = 0;

			r0 = UInt64(*(UInt32*)(&key[0]) & 0x3ffffff);
			r1 = UInt64((*(UInt32*)(&key[3]) >> 2) & 0x3ffff03);
			r2 = UInt64((*(UInt32*)(&key[6]) >> 4) & 0x3ffc0ff);
			r3 = UInt64((*(UInt32*)(&key[9]) >> 6) & 0x3f03fff);
			r4 = UInt64((*(UInt32*)(&key[12]) >> 8) & 0x00fffff);

			auto R1 = r1 * 5;
			auto R2 = r2 * 5;
			auto R3 = r3 * 5;
			auto R4 = r4 * 5;

			while (mlen >= TagSize)
			{
				// h += msg
				h0 += *(UInt32*)(&msg[0]) & 0x3ffffff;
				h1 += (*(UInt32*)(&msg[3]) >> 2) & 0x3ffffff;
				h2 += (*(UInt32*)(&msg[6]) >> 4) & 0x3ffffff;
				h3 += (*(UInt32*)(&msg[9]) >> 6) & 0x3ffffff;
				h4 += (*(UInt32*)(&msg[12]) >> 8) | (1 << 24);

				// h *= r
				auto d0 = ((UInt64)(h0)* r0) + ((UInt64)(h1)* R4) + ((UInt64)(h2)* R3) + ((UInt64)(h3)* R2) + ((UInt64)(h4)* R1);
				auto d1 = (d0 >> 26) + ((UInt64)(h0)* r1) + ((UInt64)(h1)* r0) + ((UInt64)(h2)* R4) + ((UInt64)(h3)* R3) + ((UInt64)(h4)* R2);
				auto d2 = (d1 >> 26) + ((UInt64)(h0)* r2) + ((UInt64)(h1)* r1) + ((UInt64)(h2)* r0) + ((UInt64)(h3)* R4) + ((UInt64)(h4)* R3);
				auto d3 = (d2 >> 26) + ((UInt64)(h0)* r3) + ((UInt64)(h1)* r2) + ((UInt64)(h2)* r1) + ((UInt64)(h3)* r0) + ((UInt64)(h4)* R4);
				auto d4 = (d3 >> 26) + ((UInt64)(h0)* r4) + ((UInt64)(h1)* r3) + ((UInt64)(h2)* r2) + ((UInt64)(h3)* r1) + ((UInt64)(h4)* r0);

				// h %= p
				h0 = (UInt32)(d0) & 0x3ffffff;
				h1 = (UInt32)(d1) & 0x3ffffff;
				h2 = (UInt32)(d2) & 0x3ffffff;
				h3 = (UInt32)(d3) & 0x3ffffff;
				h4 = (UInt32)(d4) & 0x3ffffff;

				h0 += (UInt32)(d4 >> 26) * 5;
				h1 += h0 >> 26;
				h0 = h0 & 0x3ffffff;

				msg += TagSize;
				mlen -= TagSize;
			}
			if (mlen > 0) {
				Byte block[TagSize] = { 0 };
				memcpy_s(block, TagSize, msg, mlen);
				block[mlen] = 0x01;

				// h += msg
				h0 += *(UInt32*)(&block[0]) & 0x3ffffff;
				h1 += (*(UInt32*)(&block[3]) >> 2) & 0x3ffffff;
				h2 += (*(UInt32*)(&block[6]) >> 4) & 0x3ffffff;
				h3 += (*(UInt32*)(&block[9]) >> 6) & 0x3ffffff;
				h4 += (*(UInt32*)(&block[12]) >> 8);

				// h *= r
				auto d0 = ((UInt64)(h0)* r0) + ((UInt64)(h1)* R4) + ((UInt64)(h2)* R3) + ((UInt64)(h3)* R2) + ((UInt64)(h4)* R1);
				auto d1 = (d0 >> 26) + ((UInt64)(h0)* r1) + ((UInt64)(h1)* r0) + ((UInt64)(h2)* R4) + ((UInt64)(h3)* R3) + ((UInt64)(h4)* R2);
				auto d2 = (d1 >> 26) + ((UInt64)(h0)* r2) + ((UInt64)(h1)* r1) + ((UInt64)(h2)* r0) + ((UInt64)(h3)* R4) + ((UInt64)(h4)* R3);
				auto d3 = (d2 >> 26) + ((UInt64)(h0)* r3) + ((UInt64)(h1)* r2) + ((UInt64)(h2)* r1) + ((UInt64)(h3)* r0) + ((UInt64)(h4)* R4);
				auto d4 = (d3 >> 26) + ((UInt64)(h0)* r4) + ((UInt64)(h1)* r3) + ((UInt64)(h2)* r2) + ((UInt64)(h3)* r1) + ((UInt64)(h4)* r0);

				// h %= p
				h0 = (UInt32)(d0) & 0x3ffffff;
				h1 = (UInt32)(d1) & 0x3ffffff;
				h2 = (UInt32)(d2) & 0x3ffffff;
				h3 = (UInt32)(d3) & 0x3ffffff;
				h4 = (UInt32)(d4) & 0x3ffffff;

				h0 += (UInt32)(d4 >> 26) * 5;
				h1 += h0 >> 26;
				h0 = h0 & 0x3ffffff;
			}


			// h %= p reduction
			h2 += h1 >> 26;
			h1 &= 0x3ffffff;
			h3 += h2 >> 26;
			h2 &= 0x3ffffff;
			h4 += h3 >> 26;
			h3 &= 0x3ffffff;
			h0 += 5 * (h4 >> 26);
			h4 &= 0x3ffffff;
			h1 += h0 >> 26;
			h0 &= 0x3ffffff;

			// h - p
			auto t0 = h0 + 5;
			auto t1 = h1 + (t0 >> 26);
			auto t2 = h2 + (t1 >> 26);
			auto t3 = h3 + (t2 >> 26);
			auto t4 = h4 + (t3 >> 26) - (1 << 26);
			t0 &= 0x3ffffff;
			t1 &= 0x3ffffff;
			t2 &= 0x3ffffff;
			t3 &= 0x3ffffff;

			// select h if h < p else h - p
			auto t_mask = (t4 >> 31) - 1;
			auto h_mask = ~t_mask;
			h0 = (h0 & h_mask) | (t0 & t_mask);
			h1 = (h1 & h_mask) | (t1 & t_mask);
			h2 = (h2 & h_mask) | (t2 & t_mask);
			h3 = (h3 & h_mask) | (t3 & t_mask);
			h4 = (h4 & h_mask) | (t4 & t_mask);

			// h %= 2^128
			h0 |= h1 << 26;
			h1 = ((h1 >> 6) | (h2 << 20));
			h2 = ((h2 >> 12) | (h3 << 14));
			h3 = ((h3 >> 18) | (h4 << 8));

			// s: the s part of the key
			// tag = (h + s) % (2^128)
			auto t = (UInt64)(h0)+(UInt64)(*(UInt32*)(&key[16]));
			h0 = (UInt32)(t);
			t = (UInt64)(h1)+(UInt64)(*(UInt32*)(&key[20])) + (t >> 32);
			h1 = (UInt32)(t);
			t = (UInt64)(h2)+(UInt64)(*(UInt32*)(&key[24])) + (t >> 32);
			h2 = (UInt32)(t);
			t = (UInt64)(h3)+(UInt64)(*(UInt32*)(&key[28])) + (t >> 32);
			h3 = (UInt32)(t);

			memcpy(&out[0], &h0, 4);
			memcpy(&out[4], &h1, 4);
			memcpy(&out[8], &h2, 4);
			memcpy(&out[12], &h3, 4);
		}
	};


	public ref class Secretbox {
	private:
		array<Byte>^ Sigma;

	public:
		const int Overhead = 16;

		Secretbox() {
			Sigma = gcnew array<Byte>{'e', 'x', 'p', 'a', 'n', 'd', ' ', '3', '2', '-', 'b', 'y', 't', 'e', ' ', 'k'};
		}

		void HSala20([Runtime::InteropServices::Out] array<Byte>^% output, array<Byte>^ input, array<Byte>^ k, array<Byte>^ c)
		{
			const int round = 20;
			if (input->Length != 16) throw gcnew ArgumentException("input buffer length must be 16");
			if (k->Length != 32) throw gcnew ArgumentException("k length must be 32");
			if (c->Length != 16) throw gcnew ArgumentException("c length must be 16");
			output = gcnew array<Byte>(32);

			pin_ptr<Byte> pinput = &input[0];
			pin_ptr<Byte> poutput = &output[0];
			pin_ptr<Byte> pk = &k[0];
			pin_ptr<Byte> pc = &c[0];

			auto x0 = *(UInt32*)&pc[0];
			auto x1 = *(UInt32*)&pk[0];
			auto x2 = *(UInt32*)&pk[4];
			auto x3 = *(UInt32*)&pk[8];
			auto x4 = *(UInt32*)&pk[12];
			auto x5 = *(UInt32*)&pc[4];
			auto x6 = *(UInt32*)&pinput[0];
			auto x7 = *(UInt32*)&pinput[4];
			auto x8 = *(UInt32*)&pinput[8];
			auto x9 = *(UInt32*)&pinput[12];
			auto x10 = *(UInt32*)&pc[8];
			auto x11 = *(UInt32*)&pk[16];
			auto x12 = *(UInt32*)&pk[20];
			auto x13 = *(UInt32*)&pk[24];
			auto x14 = *(UInt32*)&pk[28];
			auto x15 = *(UInt32*)&pc[12];

			for (int i = 0; i < round; i += 2)
			{
				auto u = x0 + x12;
				x4 ^= u << 7 | u >> (32 - 7);
				u = x4 + x0;
				x8 ^= u << 9 | u >> (32 - 9);
				u = x8 + x4;
				x12 ^= u << 13 | u >> (32 - 13);
				u = x12 + x8;
				x0 ^= u << 18 | u >> (32 - 18);

				u = x5 + x1;
				x9 ^= u << 7 | u >> (32 - 7);
				u = x9 + x5;
				x13 ^= u << 9 | u >> (32 - 9);
				u = x13 + x9;
				x1 ^= u << 13 | u >> (32 - 13);
				u = x1 + x13;
				x5 ^= u << 18 | u >> (32 - 18);

				u = x10 + x6;
				x14 ^= u << 7 | u >> (32 - 7);
				u = x14 + x10;
				x2 ^= u << 9 | u >> (32 - 9);
				u = x2 + x14;
				x6 ^= u << 13 | u >> (32 - 13);
				u = x6 + x2;
				x10 ^= u << 18 | u >> (32 - 18);

				u = x15 + x11;
				x3 ^= u << 7 | u >> (32 - 7);
				u = x3 + x15;
				x7 ^= u << 9 | u >> (32 - 9);
				u = x7 + x3;
				x11 ^= u << 13 | u >> (32 - 13);
				u = x11 + x7;
				x15 ^= u << 18 | u >> (32 - 18);

				u = x0 + x3;
				x1 ^= u << 7 | u >> (32 - 7);
				u = x1 + x0;
				x2 ^= u << 9 | u >> (32 - 9);
				u = x2 + x1;
				x3 ^= u << 13 | u >> (32 - 13);
				u = x3 + x2;
				x0 ^= u << 18 | u >> (32 - 18);

				u = x5 + x4;
				x6 ^= u << 7 | u >> (32 - 7);
				u = x6 + x5;
				x7 ^= u << 9 | u >> (32 - 9);
				u = x7 + x6;
				x4 ^= u << 13 | u >> (32 - 13);
				u = x4 + x7;
				x5 ^= u << 18 | u >> (32 - 18);

				u = x10 + x9;
				x11 ^= u << 7 | u >> (32 - 7);
				u = x11 + x10;
				x8 ^= u << 9 | u >> (32 - 9);
				u = x8 + x11;
				x9 ^= u << 13 | u >> (32 - 13);
				u = x9 + x8;
				x10 ^= u << 18 | u >> (32 - 18);

				u = x15 + x14;
				x12 ^= u << 7 | u >> (32 - 7);
				u = x12 + x15;
				x13 ^= u << 9 | u >> (32 - 9);
				u = x13 + x12;
				x14 ^= u << 13 | u >> (32 - 13);
				u = x14 + x13;
				x15 ^= u << 18 | u >> (32 - 18);
			}

			*(UInt32*)(&poutput[0]) = x0;
			*(UInt32*)(&poutput[4]) = x5;
			*(UInt32*)(&poutput[8]) = x10;
			*(UInt32*)(&poutput[12]) = x15;
			*(UInt32*)(&poutput[16]) = x6;
			*(UInt32*)(&poutput[20]) = x7;
			*(UInt32*)(&poutput[24]) = x8;
			*(UInt32*)(&poutput[28]) = x9;
		}

		void SalaCore([Runtime::InteropServices::Out] array<Byte>^% output, array<Byte>^input, array<Byte>^ k, array<Byte>^ c)
		{
			const int round = 20;
			if (input->Length != 16) throw gcnew ArgumentException("input buffer length must be 16");
			if (k->Length != 32) throw gcnew ArgumentException("k length must be 32");
			if (c->Length != 16) throw gcnew ArgumentException("c length must be 16");

			output = gcnew array<Byte>(64);

			pin_ptr<Byte> pinput = &input[0];
			pin_ptr<Byte> poutput = &output[0];
			pin_ptr<Byte> pk = &k[0];
			pin_ptr<Byte> pc = &c[0];

			UInt32 j0, j1, j2, j3, j4, j5, j6, j7, j8, j9, j10, j11, j12, j13, j14, j15;
			UInt32 x0, x1, x2, x3, x4, x5, x6, x7, x8, x9, x10, x11, x12, x13, x14, x15;

			x0 = j0 = *(UInt32*)&pc[0];
			x1 = j1 = *(UInt32*)&pk[0];
			x2 = j2 = *(UInt32*)&pk[4];
			x3 = j3 = *(UInt32*)&pk[8];
			x4 = j4 = *(UInt32*)&pk[12];
			x5 = j5 = *(UInt32*)&pc[4];
			x6 = j6 = *(UInt32*)&pinput[0];
			x7 = j7 = *(UInt32*)&pinput[4];
			x8 = j8 = *(UInt32*)&pinput[8];
			x9 = j9 = *(UInt32*)&pinput[12];
			x10 = j10 = *(UInt32*)&pc[8];
			x11 = j11 = *(UInt32*)&pk[16];
			x12 = j12 = *(UInt32*)&pk[20];
			x13 = j13 = *(UInt32*)&pk[24];
			x14 = j14 = *(UInt32*)&pk[28];
			x15 = j15 = *(UInt32*)&pc[12];

			for (int i = 0; i < round; i += 2)
			{
				auto u = x0 + x12;
				x4 ^= u << 7 | u >> (32 - 7);
				u = x4 + x0;
				x8 ^= u << 9 | u >> (32 - 9);
				u = x8 + x4;
				x12 ^= u << 13 | u >> (32 - 13);
				u = x12 + x8;
				x0 ^= u << 18 | u >> (32 - 18);

				u = x5 + x1;
				x9 ^= u << 7 | u >> (32 - 7);
				u = x9 + x5;
				x13 ^= u << 9 | u >> (32 - 9);
				u = x13 + x9;
				x1 ^= u << 13 | u >> (32 - 13);
				u = x1 + x13;
				x5 ^= u << 18 | u >> (32 - 18);

				u = x10 + x6;
				x14 ^= u << 7 | u >> (32 - 7);
				u = x14 + x10;
				x2 ^= u << 9 | u >> (32 - 9);
				u = x2 + x14;
				x6 ^= u << 13 | u >> (32 - 13);
				u = x6 + x2;
				x10 ^= u << 18 | u >> (32 - 18);

				u = x15 + x11;
				x3 ^= u << 7 | u >> (32 - 7);
				u = x3 + x15;
				x7 ^= u << 9 | u >> (32 - 9);
				u = x7 + x3;
				x11 ^= u << 13 | u >> (32 - 13);
				u = x11 + x7;
				x15 ^= u << 18 | u >> (32 - 18);

				u = x0 + x3;
				x1 ^= u << 7 | u >> (32 - 7);
				u = x1 + x0;
				x2 ^= u << 9 | u >> (32 - 9);
				u = x2 + x1;
				x3 ^= u << 13 | u >> (32 - 13);
				u = x3 + x2;
				x0 ^= u << 18 | u >> (32 - 18);

				u = x5 + x4;
				x6 ^= u << 7 | u >> (32 - 7);
				u = x6 + x5;
				x7 ^= u << 9 | u >> (32 - 9);
				u = x7 + x6;
				x4 ^= u << 13 | u >> (32 - 13);
				u = x4 + x7;
				x5 ^= u << 18 | u >> (32 - 18);

				u = x10 + x9;
				x11 ^= u << 7 | u >> (32 - 7);
				u = x11 + x10;
				x8 ^= u << 9 | u >> (32 - 9);
				u = x8 + x11;
				x9 ^= u << 13 | u >> (32 - 13);
				u = x9 + x8;
				x10 ^= u << 18 | u >> (32 - 18);

				u = x15 + x14;
				x12 ^= u << 7 | u >> (32 - 7);
				u = x12 + x15;
				x13 ^= u << 9 | u >> (32 - 9);
				u = x13 + x12;
				x14 ^= u << 13 | u >> (32 - 13);
				u = x14 + x13;
				x15 ^= u << 18 | u >> (32 - 18);
			}

			x0 += j0;
			x1 += j1;
			x2 += j2;
			x3 += j3;
			x4 += j4;
			x5 += j5;
			x6 += j6;
			x7 += j7;
			x8 += j8;
			x9 += j9;
			x10 += j10;
			x11 += j11;
			x12 += j12;
			x13 += j13;
			x14 += j14;
			x15 += j15;

			*(UInt32*)(&poutput[0]) = x0;
			*(UInt32*)(&poutput[4]) = x1;
			*(UInt32*)(&poutput[8]) = x2;
			*(UInt32*)(&poutput[12]) = x3;
			*(UInt32*)(&poutput[16]) = x4;
			*(UInt32*)(&poutput[20]) = x5;
			*(UInt32*)(&poutput[24]) = x6;
			*(UInt32*)(&poutput[28]) = x7;
			*(UInt32*)(&poutput[32]) = x8;
			*(UInt32*)(&poutput[36]) = x9;
			*(UInt32*)(&poutput[40]) = x10;
			*(UInt32*)(&poutput[44]) = x11;
			*(UInt32*)(&poutput[48]) = x12;
			*(UInt32*)(&poutput[52]) = x13;
			*(UInt32*)(&poutput[56]) = x14;
			*(UInt32*)(&poutput[60]) = x15;
		}

		void SalaCore208(array<UInt32>^ input, array<UInt32>^ output)
		{
			const int round = 8;
			if (input->Length != 16) throw gcnew ArgumentException("input buffer length must be 16");
			if (output->Length != 16) throw gcnew ArgumentException("output buffer length must be 16");

			pin_ptr<UInt32> pinput = &input[0];
			pin_ptr<UInt32> poutput = &output[0];

			UInt32 j0, j1, j2, j3, j4, j5, j6, j7, j8, j9, j10, j11, j12, j13, j14, j15;
			UInt32 x0, x1, x2, x3, x4, x5, x6, x7, x8, x9, x10, x11, x12, x13, x14, x15;

			x0 = j0 = pinput[0];
			x1 = j1 = pinput[1];
			x2 = j2 = pinput[2];
			x3 = j3 = pinput[3];
			x4 = j4 = pinput[4];
			x5 = j5 = pinput[5];
			x6 = j6 = pinput[6];
			x7 = j7 = pinput[7];
			x8 = j8 = pinput[8];
			x9 = j9 = pinput[9];
			x10 = j10 = pinput[10];
			x11 = j11 = pinput[11];
			x12 = j12 = pinput[12];
			x13 = j13 = pinput[13];
			x14 = j14 = pinput[14];
			x15 = j15 = pinput[15];

			for (int i = 0; i < round; i += 2)
			{
				auto u = x0 + x12;
				x4 ^= u << 7 | u >> (32 - 7);
				u = x4 + x0;
				x8 ^= u << 9 | u >> (32 - 9);
				u = x8 + x4;
				x12 ^= u << 13 | u >> (32 - 13);
				u = x12 + x8;
				x0 ^= u << 18 | u >> (32 - 18);

				u = x5 + x1;
				x9 ^= u << 7 | u >> (32 - 7);
				u = x9 + x5;
				x13 ^= u << 9 | u >> (32 - 9);
				u = x13 + x9;
				x1 ^= u << 13 | u >> (32 - 13);
				u = x1 + x13;
				x5 ^= u << 18 | u >> (32 - 18);

				u = x10 + x6;
				x14 ^= u << 7 | u >> (32 - 7);
				u = x14 + x10;
				x2 ^= u << 9 | u >> (32 - 9);
				u = x2 + x14;
				x6 ^= u << 13 | u >> (32 - 13);
				u = x6 + x2;
				x10 ^= u << 18 | u >> (32 - 18);

				u = x15 + x11;
				x3 ^= u << 7 | u >> (32 - 7);
				u = x3 + x15;
				x7 ^= u << 9 | u >> (32 - 9);
				u = x7 + x3;
				x11 ^= u << 13 | u >> (32 - 13);
				u = x11 + x7;
				x15 ^= u << 18 | u >> (32 - 18);

				u = x0 + x3;
				x1 ^= u << 7 | u >> (32 - 7);
				u = x1 + x0;
				x2 ^= u << 9 | u >> (32 - 9);
				u = x2 + x1;
				x3 ^= u << 13 | u >> (32 - 13);
				u = x3 + x2;
				x0 ^= u << 18 | u >> (32 - 18);

				u = x5 + x4;
				x6 ^= u << 7 | u >> (32 - 7);
				u = x6 + x5;
				x7 ^= u << 9 | u >> (32 - 9);
				u = x7 + x6;
				x4 ^= u << 13 | u >> (32 - 13);
				u = x4 + x7;
				x5 ^= u << 18 | u >> (32 - 18);

				u = x10 + x9;
				x11 ^= u << 7 | u >> (32 - 7);
				u = x11 + x10;
				x8 ^= u << 9 | u >> (32 - 9);
				u = x8 + x11;
				x9 ^= u << 13 | u >> (32 - 13);
				u = x9 + x8;
				x10 ^= u << 18 | u >> (32 - 18);

				u = x15 + x14;
				x12 ^= u << 7 | u >> (32 - 7);
				u = x12 + x15;
				x13 ^= u << 9 | u >> (32 - 9);
				u = x13 + x12;
				x14 ^= u << 13 | u >> (32 - 13);
				u = x14 + x13;
				x15 ^= u << 18 | u >> (32 - 18);
			}

			x0 += j0;
			x1 += j1;
			x2 += j2;
			x3 += j3;
			x4 += j4;
			x5 += j5;
			x6 += j6;
			x7 += j7;
			x8 += j8;
			x9 += j9;
			x10 += j10;
			x11 += j11;
			x12 += j12;
			x13 += j13;
			x14 += j14;
			x15 += j15;

			poutput[0] = x0;
			poutput[1] = x1;
			poutput[2] = x2;
			poutput[3] = x3;
			poutput[4] = x4;
			poutput[5] = x5;
			poutput[6] = x6;
			poutput[7] = x7;
			poutput[8] = x8;
			poutput[9] = x9;
			poutput[10] = x10;
			poutput[11] = x11;
			poutput[12] = x12;
			poutput[13] = x13;
			poutput[14] = x14;
			poutput[15] = x15;
		}

		void XORKeyStream([Runtime::InteropServices::Out] array<Byte>^% output, array<Byte>^ input, array<Byte>^ counter, array<Byte>^ key, int offset)
		{
			if (counter->Length != 16) throw gcnew ArgumentException("counter length must be 16");
			if (key->Length != 32) throw gcnew ArgumentException("key length must be 32");

			array<Byte>^ block;
			array<Byte>^ inCounter = gcnew array<Byte>(16);
			Array::Copy(counter, inCounter, 16);

			output = gcnew array<Byte>(input->Length - offset);

			pin_ptr<Byte> pinput = &input[0];
			pin_ptr<Byte> poutput = &output[0];
			Byte *in = &pinput[offset];
			Byte *out = poutput;


			int len = input->Length - offset;
			while (len >= 64)
			{
				SalaCore(block, inCounter, key, Sigma);
				pin_ptr<Byte> pblock = &block[0];
				for (int i = 0; i < 64; i++)
					*(out++) = (Byte)(*(in++) ^ pblock[i]);

				UInt32 u = 1;
				for (int i = 8; i < 16; i++)
				{
					u += inCounter[i];
					inCounter[i] = (Byte)(u);
					u >>= 8;
				}

				len -= 64;
			}

			if (len > 0)
			{
				SalaCore(block, inCounter, key, Sigma);
				pin_ptr<Byte> pblock = &block[0];
				for (int i = 0; i < len; i++)
					*(out++) = (Byte)(*(in++) ^ pblock[i]);
			}
		}

		void Setup([Runtime::InteropServices::Out] array<Byte>^% subkey, [Runtime::InteropServices::Out] array<Byte>^% counter, array<Byte>^ nonce, array<Byte>^ key)
		{
			if (nonce->Length != 24) throw gcnew ArgumentException("nonce length must be 24");
			if (key->Length != 32) throw gcnew ArgumentException("key length must be 32");

			array<Byte>^ hnonce = gcnew array<Byte>(16);
			counter = gcnew array<Byte>(16);

			Array::Copy(nonce, hnonce, 16);
			HSala20(subkey, hnonce, key, Sigma);

			Array::Copy(nonce, 16, counter, 0, 8);
		}

		void Seal([Runtime::InteropServices::Out] array<Byte>^% output, array<Byte>^ message, array<Byte>^ nonce, array<Byte>^ key)
		{
			if (nonce->Length != 24) throw gcnew ArgumentException("nonce length must be 24");
			if (key->Length != 32) throw gcnew ArgumentException("key length must be 32");

			array<Byte>^ subkey;
			array<Byte>^ counter;
			Setup(subkey, counter, nonce, key);

			array<Byte>^ firstBlock = gcnew array<Byte>(64);
			XORKeyStream(firstBlock, firstBlock, counter, subkey, 0);

			array<Byte>^ poly1305Key = gcnew array<Byte>(32);
			Array::Copy(firstBlock, poly1305Key, 32);

			output = gcnew array<Byte>(message->Length + Poly1305::TagSize);

			array<Byte>^ firstMessageBlock;
			if (message->Length < 32)
			{
				firstMessageBlock = gcnew array<Byte>(message->Length);
				Array::Copy(message, firstMessageBlock, message->Length);
			}
			else
			{
				firstMessageBlock = gcnew array<Byte>(32);
				Array::Copy(message, firstMessageBlock, 32);
			}

			pin_ptr<Byte> poutput = &output[0];
			pin_ptr<Byte> pfirstBlock = &firstBlock[32];
			pin_ptr<Byte> pfirstMessageBlock = &firstMessageBlock[0];
			Byte *pout = &poutput[Poly1305::TagSize];

			for (int i = 0; i < firstMessageBlock->Length; i++)
			{
				pout[i] = (Byte)(pfirstBlock[i] ^ firstMessageBlock[i]);
			}

			counter[8] = 1;
			array<Byte>^ outputbuf;
			XORKeyStream(outputbuf, message, counter, subkey, firstMessageBlock->Length);
			Array::Copy(outputbuf, 0, output, Poly1305::TagSize + firstMessageBlock->Length, outputbuf->Length);

			array<Byte>^ tag;
			Poly1305::Sum(tag, output, poly1305Key, Poly1305::TagSize);
			Array::Copy(tag, output, tag->Length);
		}

		bool Open([Runtime::InteropServices::Out] array<Byte>^% output, array<Byte>^ box, array<Byte>^ nonce, array<Byte>^ key)
		{
			if (nonce->Length != 24) throw gcnew ArgumentException("nonce length must be 24");
			if (key->Length != 32) throw gcnew ArgumentException("key length must be 32");

			array<Byte>^ subkey;
			array<Byte>^ counter;
			Setup(subkey, counter, nonce, key);

			array<Byte>^ firstBlock = gcnew array<Byte>(64);
			XORKeyStream(firstBlock, firstBlock, counter, subkey, 0);

			array<Byte>^ poly1305Key = gcnew array<Byte>(32);
			Array::Copy(firstBlock, poly1305Key, 32);

			array<Byte>^ tag = gcnew array<Byte>(Poly1305::TagSize);
			Array::Copy(box, tag, tag->Length);

			if (!Poly1305::Verify(tag, box, poly1305Key, Poly1305::TagSize))
			{
				output = nullptr;
				return false;
			}

			output = gcnew array<Byte>(box->Length - Poly1305::TagSize);

			array<Byte>^ firstMessageBlock;
			if (output->Length < 32)
			{
				firstMessageBlock = gcnew array<Byte>(output->Length);
				Array::Copy(box, Poly1305::TagSize, firstMessageBlock, 0, firstMessageBlock->Length);
			}
			else
			{
				firstMessageBlock = gcnew array<Byte>(32);
				Array::Copy(box, Poly1305::TagSize, firstMessageBlock, 0, firstMessageBlock->Length);
			}


			pin_ptr<Byte> poutput = &output[0];
			pin_ptr<Byte> pfirstBlock = &firstBlock[32];
			pin_ptr<Byte> pfirstMessageBlock = &firstMessageBlock[0];
			Byte *pout = &poutput[0];

			for (int i = 0; i < firstMessageBlock->Length; i++)
			{
				pout[i] = (Byte)(pfirstBlock[i] ^ firstMessageBlock[i]);
			}

			counter[8] = 1;
			array<Byte>^ outputbuf;
			XORKeyStream(outputbuf, box, counter, subkey, firstMessageBlock->Length + Poly1305::TagSize);
			Array::Copy(outputbuf, 0, output, firstMessageBlock->Length, outputbuf->Length);
			return true;
		}

	};
}
